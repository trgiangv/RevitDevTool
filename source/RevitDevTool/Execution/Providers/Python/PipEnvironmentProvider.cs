using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using CliWrap;
using RevitDevTool.Execution.Services;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.Execution.Providers.Python;

/// <summary>
/// Pip-based Python environment provider for restricted enterprise environments
/// where pixi.exe cannot execute due to security policies.
/// Discovers the CPython distribution shipped with pyRevit (cengines directory),
/// bootstraps pip, and uses <c>python.exe -m pip</c> for package management.
/// </summary>
public sealed class PipEnvironmentProvider : PyEnvironmentProvider
{
    public override PythonBackend Backend => PythonBackend.Pip;

    public override async Task SetupEnvironmentAsync()
    {
        if (!IsEnvironmentReady())
        {
            PythonHomePath = await DiscoverPyRevitAsync().ConfigureAwait(false);
            RemovePthFile(PythonHomePath);

            if (!await IsPipAvailableAsync().ConfigureAwait(false))
                await BootstrapPipAsync().ConfigureAwait(false);
        }

        await EnsureRequirePackagesAsync().ConfigureAwait(false);

        PythonEmbedded.EnsureExtracted();
    }

    /// <summary>
    /// Locates pyrevit.exe on PATH, navigates to <c>..\cengines</c>,
    /// and picks the first CPython engine directory containing python.exe.
    /// </summary>
    private static Task<string> DiscoverPyRevitAsync()
    {
        var pyrevitExe = FindOnPath("pyrevit.exe");
        if (pyrevitExe is null)
            throw new FileNotFoundException(
                "pyrevit.exe not found on PATH. Ensure pyRevit CLI is installed.");

        var binDir = Path.GetDirectoryName(pyrevitExe)!;
        var cenginesDir = Path.Combine(binDir, "cengines");

        if (!Directory.Exists(cenginesDir))
            throw new DirectoryNotFoundException(
                $"cengines directory not found at: {cenginesDir}");

        var engineDir = Directory.EnumerateDirectories(cenginesDir, "CPY*")
            .FirstOrDefault(d => File.Exists(Path.Combine(d, "python.exe")));

        if (engineDir is null)
            throw new FileNotFoundException(
                $"No CPython engine with python.exe found in: {cenginesDir}");

        Trace.TraceInformation($"[Pip] Discovered pyRevit CPython at: {engineDir}");
        return Task.FromResult(engineDir);
    }

    private static string? FindOnPath(string fileName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar)) return null;

        foreach (var dir in pathVar.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var full = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(full)) return full;
        }

        return null;
    }

    private async Task EnsureRequirePackagesAsync()
    {
        var installed = await RunPipListAsync().ConfigureAwait(false);
        var missing = RequirePackages
            .Where(kv => !installed.Contains(CanonicalizePackageName(kv.Key)))
            .Select(kv => kv.Value)
            .ToList();

        if (missing.Count == 0)
        {
            Debug.WriteLine("[Pip] All required packages already installed.");
            return;
        }

        Trace.TraceInformation($"[Pip] Installing required packages: {string.Join(", ", missing)}");

        var args = new List<string> { "-m", "pip", "install", "--prefer-binary", "--no-warn-script-location" };
        args.AddRange(missing);

        var result = await Cli.Wrap(PythonExe)
            .WithArguments(args)
            .WithWorkingDirectory(PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Debug.WriteLine($"[pip] {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Debug.WriteLine($"[pip] {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new Exception($"Failed to install required packages: {string.Join(", ", missing)}");

        Trace.TraceInformation("[Pip] Required packages installed.");
    }

    public override async Task InstallPackagesAsync(
        IEnumerable<string> packages,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var list = packages.ToList();
        if (list.Count == 0) return;

        progress.Report($"Installing {list.Count} package(s) via pip: {string.Join(", ", list)}");

        var (succeeded, failed) = await TryPipInstallBatchAsync(
            list, progress, cancellationToken).ConfigureAwait(false);

        if (succeeded.Count > 0 && failed.Count > 0)
            progress.Report($"pip: {string.Join(", ", succeeded)}");

        if (failed.Count > 0)
            throw new Exception($"Failed to install the following package(s): {string.Join(", ", failed)}");

        progress.Report($"All {list.Count} package(s) installed via pip.");
    }

    private async Task<HashSet<string>> RunPipListAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnvironmentReady()) return [];

        var stdout = new StringBuilder();

        var result = await Cli.Wrap(PythonExe)
            .WithArguments(["-m", "pip", "list", "--format=json"])
            .WithWorkingDirectory(PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0) return [];

        var json = stdout.ToString().Trim();
        return string.IsNullOrEmpty(json) ? [] : ParsePipListJson(json);
    }

    private static HashSet<string> ParsePipListJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var name = entry.GetProperty("name").GetString();
            if (!string.IsNullOrEmpty(name))
                names.Add(CanonicalizePackageName(name!));
        }

        return names;
    }

    private static string CanonicalizePackageName(string name)
        => name.ToLowerInvariant().Replace('_', '-').Replace('.', '-');

    private static void RemovePthFile(string targetDir)
    {
        var pthFile = Directory.EnumerateFiles(targetDir, "python*._pth").FirstOrDefault();
        if (pthFile is null) return;

        File.Delete(pthFile);
        Debug.WriteLine($"[Pip] Removed {Path.GetFileName(pthFile)} to enable site-packages.");
    }

    private async Task<bool> IsPipAvailableAsync()
    {
        var result = await Cli.Wrap(PythonExe)
            .WithArguments(["-m", "pip", "--version"])
            .WithWorkingDirectory(PythonHome)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0) return false;

        Debug.WriteLine("[Pip] pip already available, skipping bootstrap.");
        return true;
    }

    /// <summary>
    /// Bootstraps pip: tries ensurepip first, falls back to get-pip.py
    /// for embedded distributions where ensurepip is absent.
    /// </summary>
    private async Task BootstrapPipAsync()
    {
        if (await TryEnsurepipAsync().ConfigureAwait(false))
            return;

        Trace.TraceInformation("[Pip] ensurepip unavailable, falling back to get-pip.py...");
        await GetPipAsync().ConfigureAwait(false);
    }

    private async Task<bool> TryEnsurepipAsync()
    {
        Debug.WriteLine("[Pip] Trying ensurepip...");

        var result = await Cli.Wrap(PythonExe)
            .WithArguments(["-m", "ensurepip", "--upgrade"])
            .WithWorkingDirectory(PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Debug.WriteLine($"[ensurepip] {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Debug.WriteLine($"[ensurepip] {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            Debug.WriteLine($"[Pip] ensurepip failed (exit {result.ExitCode}).");
            return false;
        }

        Trace.TraceInformation("[Pip] pip bootstrapped via ensurepip.");
        return true;
    }

    private async Task GetPipAsync()
    {
        const string getPipUrl = "https://bootstrap.pypa.io/get-pip.py";
        var getPipPath = Path.Combine(PythonHome, "get-pip.py");

        if (!File.Exists(getPipPath))
        {
            Debug.WriteLine("[Pip] Downloading get-pip.py...");
            var script = await NetworkService.GetStringAsync(getPipUrl).ConfigureAwait(false);
            await File.WriteAllTextAsync(getPipPath, script).ConfigureAwait(false);
        }

        var result = await Cli.Wrap(PythonExe)
            .WithArguments([getPipPath, "--no-warn-script-location"])
            .WithWorkingDirectory(PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Debug.WriteLine($"[get-pip] {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Debug.WriteLine($"[get-pip] {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"get-pip.py failed (exit {result.ExitCode}). " +
                "Cannot bootstrap pip into pyRevit CPython. Check network connectivity.");

        Trace.TraceInformation("[Pip] pip bootstrapped via get-pip.py.");
    }

    private async Task<(List<string> Succeeded, List<string> Failed)> TryPipInstallBatchAsync(
        List<string> packages,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var args = new List<string> { "-m", "pip", "install", "--prefer-binary" };
        args.AddRange(packages);

        var batchResult = await Cli.Wrap(PythonExe)
            .WithArguments(args)
            .WithWorkingDirectory(PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => progress.Report($"  {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => progress.Report($"  {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);

        if (batchResult.ExitCode == 0)
            return (packages, []);

        var succeeded = new List<string>();
        var failed = new List<string>();

        foreach (var pkg in packages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var singleResult = await Cli.Wrap(PythonExe)
                .WithArguments(["-m", "pip", "install", "--prefer-binary", pkg])
                .WithWorkingDirectory(PythonHome)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line => progress.Report($"  {line}")))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line => progress.Report($"  {line}")))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(cancellationToken).ConfigureAwait(false);

            if (singleResult.ExitCode == 0)
                succeeded.Add(pkg);
            else
                failed.Add(pkg);
        }

        return (succeeded, failed);
    }
}
