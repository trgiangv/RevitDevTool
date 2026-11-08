using CliWrap;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class ParserScriptTests
{
    private const string PythonVersion = "3.14.7";
    private const string PythonDownloadUrl = $"https://www.python.org/ftp/python/{PythonVersion}/python-{PythonVersion}-embed-amd64.zip";
    private const string PythonPthFile = "python314._pth";

    private static readonly string PythonHome = Path.Combine(Path.GetTempPath(), "RevitDevTool-ParserPython", PythonVersion);
    private static readonly string PythonExe = Path.Combine(PythonHome, "python.exe");
    private static readonly string ReadyMarker = Path.Combine(PythonHome, ".ready");
    private static string? _skipReason;

    private static string ParserScriptPath => Path.Combine(FindRepositoryRoot(), "source", "DevTools.Execution", "Resources", "scripts", "Parser.py");
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [ClassInitialize]
    public static async Task InitializeAsync(TestContext _)
    {
        if (File.Exists(ReadyMarker))
            return;

        try
        {
            if (Directory.Exists(PythonHome))
                Directory.Delete(PythonHome, true);
            Directory.CreateDirectory(PythonHome);

            await DownloadAndExtractPythonAsync();
            File.Delete(Path.Combine(PythonHome, PythonPthFile));
            await BootstrapPipAsync();
            var packaging = await RunPythonAsync("-m", "pip", "install", "--prefer-binary", "packaging");
            if (packaging.ExitCode != 0)
                throw new InvalidOperationException($"Failed to install packaging: {packaging.Stderr}");

            File.WriteAllText(ReadyMarker, PythonVersion);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or SocketException)
        {
            _skipReason = $"Embedded Python download failed: {ex.Message}";
        }
    }

    [TestInitialize]
    public void EnsurePythonReady()
    {
        if (_skipReason is not null)
            Assert.Inconclusive(_skipReason);
    }

    [TestMethod]
    public async Task Parser_DetectsDependencies_FromPep723Script()
    {
        var listJson = """
            [
              {"name": "packaging", "version": "26.0", "kind": "conda"}
            ]
            """;

        var result = await RunParserAsync(Fixture("pep723_sample.py"), listJson);

        Assert.AreEqual(0, result.ExitCode, result.Stderr);
        var parsed = JsonDocument.Parse(result.Stdout).RootElement;
        Assert.AreEqual(">=3.11", parsed.GetProperty("requires_python").GetString());
        var packages = ToInstall(parsed);
        Assert.IsTrue(packages.Exists(p => p.StartsWith("requests", StringComparison.Ordinal)));
        Assert.IsFalse(packages.Exists(p => p == "packaging"));
    }

    [TestMethod]
    public async Task Parser_WithPixiListJson_SkipsAlreadyInstalled()
    {
        var listJson = """
            [
              {"name": "packaging", "version": "26.0", "kind": "conda"},
              {"name": "pip", "version": "25.0", "kind": "pypi"}
            ]
            """;

        var result = await RunParserAsync(Fixture("pep723_sample.py"), listJson);

        Assert.AreEqual(0, result.ExitCode, result.Stderr);
        var packages = ToInstall(JsonDocument.Parse(result.Stdout).RootElement);
        Assert.IsFalse(packages.Exists(p => p.Equals("packaging", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(packages.Exists(p => p.StartsWith("requests", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task Parser_WithCondaGitDescribeVersion_DoesNotFail()
    {
        var listJson = """
            [
              {"name": "libwinpthread", "version": "12.0.0.r4.gg4f2fc60ca", "kind": "conda"},
              {"name": "packaging", "version": "26.0", "kind": "conda"}
            ]
            """;

        var result = await RunParserAsync(Fixture("pep723_sample.py"), listJson);

        Assert.AreEqual(0, result.ExitCode, result.Stderr);
        Assert.DoesNotContain("Invalid specifier", result.Stderr, StringComparison.Ordinal);
        var packages = ToInstall(JsonDocument.Parse(result.Stdout).RootElement);
        Assert.IsFalse(packages.Exists(p => p.Equals("packaging", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(packages.Exists(p => p.StartsWith("requests", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task Parser_WithEmptyPixiListJson_InstallsAllPep723Deps()
    {
        var result = await RunParserAsync(Fixture("pep723_sample.py"), "[]");

        Assert.AreEqual(0, result.ExitCode, result.Stderr);
        var packages = ToInstall(JsonDocument.Parse(result.Stdout).RootElement);
        Assert.IsTrue(packages.Exists(p => p.StartsWith("requests", StringComparison.Ordinal)));
        Assert.IsTrue(packages.Exists(p => p == "packaging"));
    }

    [TestMethod]
    public async Task Parser_ReturnsEmpty_WhenNoPep723Block()
    {
        var result = await RunParserAsync(Fixture("pep723_no_deps.py"), string.Empty);

        Assert.AreEqual(0, result.ExitCode, result.Stderr);
        Assert.IsEmpty(ToInstall(JsonDocument.Parse(result.Stdout).RootElement));
    }

    [TestMethod]
    public async Task Parser_ReturnsAllDeps_WhenPixiTomlEmpty()
    {
        var result = await RunParserAsync(Fixture("pep723_sample.py"), string.Empty);

        Assert.AreEqual(0, result.ExitCode, result.Stderr);
        var packages = ToInstall(JsonDocument.Parse(result.Stdout).RootElement);
        Assert.IsTrue(packages.Exists(p => p.StartsWith("requests", StringComparison.Ordinal)));
        Assert.IsTrue(packages.Exists(p => p == "packaging"));
    }

    private static List<string> ToInstall(JsonElement root) =>
        root.GetProperty("to_install").EnumerateArray().Select(e => e.GetString()!).ToList();

    private static Task<ProcessResult> RunPythonAsync(params string[] args) => RunAsync(args, null);

    private static Task<ProcessResult> RunParserAsync(string scriptPath, string listJson) =>
        RunAsync([ParserScriptPath, scriptPath], listJson);

    private static async Task<ProcessResult> RunAsync(string[] args, string? input)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var command = Cli.Wrap(PythonExe)
            .WithArguments(args)
            .WithWorkingDirectory(PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
            .WithValidation(CommandResultValidation.None);
        if (input is not null)
            command = command.WithStandardInputPipe(PipeSource.FromString(input));

        var result = await command.ExecuteAsync();
        return new ProcessResult(result.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim());
    }

    private static async Task DownloadAndExtractPythonAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.Add("User-Agent", "RevitDevTool-Tests");
        var zip = Path.Combine(Path.GetTempPath(), $"python-test-{Guid.NewGuid():N}.zip");
        try
        {
            await File.WriteAllBytesAsync(zip, await http.GetByteArrayAsync(PythonDownloadUrl));
            System.IO.Compression.ZipFile.ExtractToDirectory(zip, PythonHome);
        }
        finally
        {
            File.Delete(zip);
        }
    }

    private static async Task BootstrapPipAsync()
    {
        if ((await RunPythonAsync("-m", "ensurepip", "--upgrade")).ExitCode == 0)
            return;

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var getPip = Path.Combine(PythonHome, "get-pip.py");
        await File.WriteAllTextAsync(getPip, await http.GetStringAsync("https://bootstrap.pypa.io/get-pip.py"));
        var result = await RunPythonAsync(getPip);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"get-pip.py failed: {result.Stderr}");
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "RevitDevTool.slnx")))
                return current.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the RevitDevTool repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);
}
