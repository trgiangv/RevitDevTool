using System.Text;
using System.Text.Json;
using CliWrap;

namespace DevTools.Execution.Tests;

/// <summary>
/// Opt-in integration tests for the Pip fallback provider.
/// Default host Python is <c>%AppData%/RevitDevTool/pixi-env</c>
/// (<see cref="DevTools.Execution.Providers.Python.PixiEnvironmentProvider"/>).
/// Set <c>RUN_PIP_ENV_TESTS=1</c> to download an isolated embeddable CPython.
/// </summary>
public sealed class PipEnvironmentTests : IAsyncLifetime, IDisposable
{
    private const string PythonVersion = "3.13.12";
    private const string PythonDownloadUrl = $"https://www.python.org/ftp/python/{PythonVersion}/python-{PythonVersion}-embed-amd64.zip";
    private const string PythonPthFile = "python313._pth";

    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), $"RevitDevTool-PipTest-{Guid.NewGuid():N}");
    private string PythonHome => Path.Combine(_testRoot, "envs", "default");
    private string PythonExe => Path.Combine(PythonHome, "python.exe");
    private string ParserScriptPath => Path.Combine(FindRepositoryRoot(), "source", "DevTools.Execution", "Resources", "scripts", "Parser.py");
    private string FixturesPath => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public async ValueTask InitializeAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_PIP_ENV_TESTS"), "1", StringComparison.Ordinal))
        {
            Assert.Skip(
                "Pip embed download is opt-in. Host Python is %AppData%/RevitDevTool/pixi-env. Set RUN_PIP_ENV_TESTS=1 to run this suite.");
        }

        Directory.CreateDirectory(PythonHome);

        if (!File.Exists(PythonExe))
        {
            await DownloadAndExtractPythonAsync();
            RemovePthFile();
            await BootstrapPipAsync();
            await InstallPackagingAsync();
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    // ─── Setup Tests ─────────────────────────────────────────────────────

    [Fact]
    public void Setup_PythonExeExists()
    {
        Assert.True(File.Exists(PythonExe), $"python.exe not found at {PythonExe}");
    }

    [Fact]
    public void Setup_PthFileRemoved()
    {
        var pthPath = Path.Combine(PythonHome, PythonPthFile);
        Assert.False(File.Exists(pthPath), $"{PythonPthFile} should be removed to enable site-packages");
    }

    [Fact]
    public async Task Setup_PipIsAvailable()
    {
        var result = await RunPythonAsync("-m", "pip", "--version");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("pip", result.Stdout);
    }

    [Fact]
    public async Task Setup_PythonVersionMatches()
    {
        var result = await RunPythonAsync("--version");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(PythonVersion, result.Stdout);
    }

    // ─── PEP 723 Parsing Tests ───────────────────────────────────────────

    [Fact]
    public async Task Parser_DetectsDependencies_FromPep723Script()
    {
        var scriptPath = Path.Combine(FixturesPath, "pep723_sample.py");
        // Production feeds pixi/pip list JSON via PyEnvironmentProvider.GetListJsonAsync.
        var listJson = """
            [
              {"name": "packaging", "version": "26.0", "kind": "conda"}
            ]
            """;

        var result = await RunParserAsync(scriptPath, listJson);

        Assert.Equal(0, result.ExitCode);

        var parsed = JsonDocument.Parse(result.Stdout);
        var toInstall = parsed.RootElement.GetProperty("to_install");
        var requiresPython = parsed.RootElement.GetProperty("requires_python").GetString();

        Assert.Equal(">=3.11", requiresPython);

        var packages = toInstall.EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.Contains(packages, p => p.StartsWith("requests"));
        Assert.DoesNotContain(packages, p => p == "packaging");
    }

    [Fact]
    public async Task Parser_WithPixiListJson_SkipsAlreadyInstalled()
    {
        // Same shape as `pixi list --json` — installed-state path (decision 0014-B)
        var listJson = """
            [
              {"name": "packaging", "version": "26.0", "kind": "conda"},
              {"name": "pip", "version": "25.0", "kind": "pypi"}
            ]
            """;

        var result = await RunParserAsync(Path.Combine(FixturesPath, "pep723_sample.py"), listJson);
        Assert.Equal(0, result.ExitCode);

        var packages = JsonDocument.Parse(result.Stdout).RootElement
            .GetProperty("to_install").EnumerateArray()
            .Select(e => e.GetString()!).ToList();

        Assert.DoesNotContain(packages, p => p.Equals("packaging", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(packages, p => p.StartsWith("requests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Parser_WithCondaGitDescribeVersion_DoesNotFail()
    {
        // conda-forge win-64 libwinpthread uses git-describe versions that are
        // not PEP 440; pixi list --json still emits them. Parser must skip the
        // invalid specifier and keep resolving PEP 723 deps.
        var listJson = """
            [
              {"name": "libwinpthread", "version": "12.0.0.r4.gg4f2fc60ca", "kind": "conda"},
              {"name": "packaging", "version": "26.0", "kind": "conda"}
            ]
            """;

        var result = await RunParserAsync(Path.Combine(FixturesPath, "pep723_sample.py"), listJson);
        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("Invalid specifier", result.Stderr, StringComparison.Ordinal);

        var packages = JsonDocument.Parse(result.Stdout).RootElement
            .GetProperty("to_install").EnumerateArray()
            .Select(e => e.GetString()!).ToList();

        Assert.DoesNotContain(packages, p => p.Equals("packaging", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(packages, p => p.StartsWith("requests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Parser_WithEmptyPixiListJson_InstallsAllPep723Deps()
    {
        var result = await RunParserAsync(Path.Combine(FixturesPath, "pep723_sample.py"), "[]");
        Assert.Equal(0, result.ExitCode);

        var packages = JsonDocument.Parse(result.Stdout).RootElement
            .GetProperty("to_install").EnumerateArray()
            .Select(e => e.GetString()!).ToList();

        Assert.Contains(packages, p => p.StartsWith("requests"));
        Assert.Contains(packages, p => p == "packaging");
    }

    [Fact]
    public async Task Parser_ReturnsEmpty_WhenNoPep723Block()
    {
        var scriptPath = Path.Combine(FixturesPath, "pep723_no_deps.py");

        var result = await RunParserAsync(scriptPath, string.Empty);

        Assert.Equal(0, result.ExitCode);

        var parsed = JsonDocument.Parse(result.Stdout);
        var toInstall = parsed.RootElement.GetProperty("to_install");
        Assert.Equal(0, toInstall.GetArrayLength());
    }

    [Fact]
    public async Task Parser_ReturnsAllDeps_WhenPixiTomlEmpty()
    {
        var scriptPath = Path.Combine(FixturesPath, "pep723_sample.py");

        var result = await RunParserAsync(scriptPath, string.Empty);

        Assert.Equal(0, result.ExitCode);

        var parsed = JsonDocument.Parse(result.Stdout);
        var packages = parsed.RootElement.GetProperty("to_install")
            .EnumerateArray().Select(e => e.GetString()!).ToList();

        Assert.Contains(packages, p => p.StartsWith("requests"));
        Assert.Contains(packages, p => p == "packaging");
    }

    // ─── Package Install Tests ───────────────────────────────────────────

    [Fact]
    public async Task PipInstall_SinglePackage_Succeeds()
    {
        var result = await RunPythonAsync("-m", "pip", "install", "--prefer-binary", "six");
        Assert.Equal(0, result.ExitCode);

        var check = await RunPythonAsync("-c", "import six; print(six.__version__)");
        Assert.Equal(0, check.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(check.Stdout));
    }

    [Fact]
    public async Task PipInstall_MultiplePackages_Succeeds()
    {
        var result = await RunPythonAsync("-m", "pip", "install", "--prefer-binary", "six", "idna");
        Assert.Equal(0, result.ExitCode);

        var checkSix = await RunPythonAsync("-c", "import six; print(six.__version__)");
        var checkIdna = await RunPythonAsync("-c", "import idna; print(idna.__version__)");
        Assert.Equal(0, checkSix.ExitCode);
        Assert.Equal(0, checkIdna.ExitCode);
    }

    [Fact]
    public async Task PipInstall_NonExistentPackage_Fails()
    {
        var result = await RunPythonAsync("-m", "pip", "install", "--prefer-binary", "this-package-does-not-exist-xyz-999");
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task PipList_ReturnsJson()
    {
        var result = await RunPythonAsync("-m", "pip", "list", "--format=json");
        Assert.Equal(0, result.ExitCode);

        var doc = JsonDocument.Parse(result.Stdout);
        Assert.True(doc.RootElement.GetArrayLength() > 0);

        var names = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()!)
            .ToList();
        Assert.Contains(names, n => n.Equals("pip", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PipInstall_RequiredPackages_AllSucceed()
    {
        string[] requiredPackages = ["mcp", "debugpy", "packaging"];

        var result = await RunPythonAsync(
            ["-m", "pip", "install", "--prefer-binary", .. requiredPackages]);
        Assert.Equal(0, result.ExitCode);

        foreach (var pkg in requiredPackages)
        {
            var check = await RunPythonAsync("-c", $"import {pkg}; print('{pkg} ok')");
            Assert.True(check.ExitCode == 0, $"Failed to import {pkg}: {check.Stderr}");
        }
    }

    // ─── Pip List + Dependency Resolution Tests ─────────────────────────

    [Fact]
    public async Task PipListNames_ContainsInstalledPackage()
    {
        await RunPythonAsync("-m", "pip", "install", "--prefer-binary", "six");

        var installed = await GetInstalledPackageNamesAsync();

        Assert.Contains("six", installed);
        Assert.DoesNotContain("this-does-not-exist", installed);
    }

    [Fact]
    public async Task PipListNames_ExcludesNotInstalled()
    {
        var installed = await GetInstalledPackageNamesAsync();

        Assert.DoesNotContain("requests", installed);
    }

    [Fact]
    public async Task Resolve_FiltersOutAlreadyInstalledDeps()
    {
        await RunPythonAsync("-m", "pip", "install", "--prefer-binary", "requests", "packaging");

        var allDeps = await GetAllPep723DepsAsync(Path.Combine(FixturesPath, "pep723_sample.py"));
        var installed = await GetInstalledPackageNamesAsync();

        var toInstall = allDeps
            .Where(dep => !IsAlreadyInstalled(dep, installed))
            .ToList();

        Assert.DoesNotContain(toInstall, p => ExtractPackageName(p) == "requests");
        Assert.DoesNotContain(toInstall, p => ExtractPackageName(p) == "packaging");
    }

    [Fact]
    public async Task Resolve_DetectsNewDepsNotYetInstalled()
    {
        var allDeps = await GetAllPep723DepsAsync(Path.Combine(FixturesPath, "pep723_sample.py"));
        var installed = await GetInstalledPackageNamesAsync();

        var toInstall = allDeps
            .Where(dep => !IsAlreadyInstalled(dep, installed))
            .ToList();

        Assert.Contains(toInstall, p => ExtractPackageName(p) == "requests");
    }

    [Fact]
    public async Task EndToEnd_InstallMissingDeps_ThenAllResolved()
    {
        var scriptPath = Path.Combine(FixturesPath, "pep723_sample.py");

        var allDeps = await GetAllPep723DepsAsync(scriptPath);
        var installed = await GetInstalledPackageNamesAsync();

        var toInstall = allDeps
            .Where(dep => !IsAlreadyInstalled(dep, installed))
            .ToList();

        if (toInstall.Count > 0)
        {
            var installArgs = new List<string> { "-m", "pip", "install", "--prefer-binary" };
            installArgs.AddRange(toInstall);
            var installResult = await RunPythonAsync(installArgs.ToArray());
            Assert.Equal(0, installResult.ExitCode);
        }

        var postInstalled = await GetInstalledPackageNamesAsync();
        var remaining = allDeps
            .Where(dep => !IsAlreadyInstalled(dep, postInstalled))
            .ToList();

        Assert.Empty(remaining);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private async Task<HashSet<string>> GetInstalledPackageNamesAsync()
    {
        var result = await RunPythonAsync("-m", "pip", "list", "--format=json");
        Assert.Equal(0, result.ExitCode);

        using var doc = JsonDocument.Parse(result.Stdout);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var name = entry.GetProperty("name").GetString();
            if (!string.IsNullOrEmpty(name))
                names.Add(CanonicalizePackageName(name));
        }
        return names;
    }

    private async Task<List<string>> GetAllPep723DepsAsync(string scriptPath)
    {
        var result = await RunParserAsync(scriptPath, string.Empty);
        Assert.Equal(0, result.ExitCode);

        var parsed = JsonDocument.Parse(result.Stdout);
        return parsed.RootElement.GetProperty("to_install")
            .EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    private static bool IsAlreadyInstalled(string depSpec, HashSet<string> installed)
    {
        var name = ExtractPackageName(depSpec);
        return installed.Contains(CanonicalizePackageName(name));
    }

    private static string ExtractPackageName(string depSpec)
    {
        var i = 0;
        while (i < depSpec.Length && depSpec[i] != '>' && depSpec[i] != '<'
               && depSpec[i] != '=' && depSpec[i] != '!' && depSpec[i] != '~'
               && depSpec[i] != '[' && depSpec[i] != ';' && depSpec[i] != ' ')
        {
            i++;
        }
        return depSpec[..i];
    }

    private static string CanonicalizePackageName(string name)
        => name.ToLowerInvariant().Replace('_', '-').Replace('.', '-');

    private async Task<ProcessResult> RunPythonAsync(params string[] args)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var result = await Cli.Wrap(PythonExe)
            .WithArguments(args)
            .WithWorkingDirectory(PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        return new ProcessResult(result.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim());
    }

    private async Task<ProcessResult> RunParserAsync(string scriptPath, string pixiTomlContent)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var result = await Cli.Wrap(PythonExe)
            .WithArguments([ParserScriptPath, scriptPath])
            .WithWorkingDirectory(PythonHome)
            .WithStandardInputPipe(PipeSource.FromString(pixiTomlContent))
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        return new ProcessResult(result.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim());
    }

    private async Task DownloadAndExtractPythonAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.Add("User-Agent", "RevitDevTool-Tests");

        var zipBytes = await http.GetByteArrayAsync(PythonDownloadUrl);
        var tempZip = Path.Combine(Path.GetTempPath(), $"python-test-{Guid.NewGuid():N}.zip");

        try
        {
            await File.WriteAllBytesAsync(tempZip, zipBytes);
            System.IO.Compression.ZipFile.ExtractToDirectory(tempZip, PythonHome);
        }
        finally
        {
            if (File.Exists(tempZip)) File.Delete(tempZip);
        }
    }

    private void RemovePthFile()
    {
        var pthPath = Path.Combine(PythonHome, PythonPthFile);
        if (File.Exists(pthPath)) File.Delete(pthPath);
    }

    private async Task BootstrapPipAsync()
    {
        var result = await RunPythonAsync("-m", "ensurepip", "--upgrade");
        if (result.ExitCode == 0) return;

        // Embedded Python strips ensurepip — fall back to get-pip.py
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var getPipScript = await http.GetStringAsync("https://bootstrap.pypa.io/get-pip.py");
        var getPipPath = Path.Combine(PythonHome, "get-pip.py");
        await File.WriteAllTextAsync(getPipPath, getPipScript);

        var pipResult = await RunPythonAsync(getPipPath);
        if (pipResult.ExitCode != 0)
            throw new Exception($"get-pip.py failed: {pipResult.Stderr}");
    }

    private async Task InstallPackagingAsync()
    {
        var result = await RunPythonAsync("-m", "pip", "install", "--prefer-binary", "packaging");
        if (result.ExitCode != 0)
            throw new Exception($"Failed to install packaging: {result.Stderr}");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RevitDevTool.slnx"))
                || File.Exists(Path.Combine(current.FullName, "RevitDevTool.sln")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the RevitDevTool repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);
}
