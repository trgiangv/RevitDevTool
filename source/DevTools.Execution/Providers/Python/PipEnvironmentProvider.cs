using System.IO;
using System.Text;
using CliWrap;
using DevTools.Execution.Services;
using Microsoft.Extensions.Logging;
using ZLogger;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Execution.Providers.Python;

/// <summary>
/// Pip-based Python environment provider for restricted enterprise environments
/// where pixi.exe cannot execute due to security policies.
/// Discovers the CPython distribution shipped with pyRevit (cengines directory),
/// bootstraps pip, and uses <c>python.exe -m pip</c> for package management.
/// Policy: skip if listed; otherwise pip install (single channel — no search-first).
/// </summary>
public sealed class PipEnvironmentProvider(ILogger<PipEnvironmentProvider> logger) : PyEnvironmentProvider
{
    public override PythonBackend Backend => PythonBackend.Pip;

    protected override Task<string> ResolvePythonHomeAsync()
        => DiscoverPyRevitAsync();

    public override async Task SetupEnvironmentAsync()
    {
        await EnsurePythonHomeAsync().ConfigureAwait(false);
        RemovePthFile(PythonHome);

        if (!await IsPipAvailableAsync().ConfigureAwait(false))
            await BootstrapPipAsync().ConfigureAwait(false);

        if (!IsEnvironmentReady())
            throw new InvalidOperationException("Python environment is not ready after setup.");

        await EnsureRequirePackagesAsync().ConfigureAwait(false);

        PythonEmbedded.EnsureExtracted();
    }

    /// <summary>
    /// Queries attached pyRevit clones and picks the first CPython engine
    /// under <c>bin\cengines</c> that contains python.exe.
    /// </summary>
    private async Task<string> DiscoverPyRevitAsync()
    {
        var clonePaths = await GetAttachedClonePathsAsync().ConfigureAwait(false);
        var candidateDirectories = clonePaths
            .Select(path => Path.Combine(path, "bin", "cengines"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var cenginesDir in candidateDirectories.Where(Directory.Exists))
        {
            var engineDir = Directory.EnumerateDirectories(cenginesDir, "CPY*")
                .FirstOrDefault(d => File.Exists(Path.Combine(d, "python.exe")));

            if (engineDir is null)
                continue;

            logger.ZLogInformation($"[Pip] Discovered pyRevit CPython at: {engineDir}");
            return engineDir;
        }

        throw new DirectoryNotFoundException(
            $"cengines directory not found. Searched: {string.Join(", ", candidateDirectories.Distinct(StringComparer.OrdinalIgnoreCase))}");
    }

    private static async Task<List<string>> GetAttachedClonePathsAsync()
    {
        var stdout = new StringBuilder();

        var result = await Cli.Wrap("pyrevit.exe")
            .WithArguments("attached")
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to query pyRevit attachments. Exit code: {result.ExitCode}");
        }

        var paths = new List<string>();

        foreach (var line in stdout.ToString().Split(Environment.NewLine))
        {
            const string marker = "Path: \"";
            var start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                continue;

            start += marker.Length;
            var end = line.IndexOf('"', start);
            if (end <= start)
                continue;

            var path = line[start..end];
            if (!string.IsNullOrWhiteSpace(path))
                paths.Add(path);
        }

        if (paths.Count == 0)
            throw new DirectoryNotFoundException("No attached pyRevit clone paths were reported by 'pyrevit attached'.");

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task EnsureRequirePackagesAsync()
    {
        var installed = await GetInstalledNamesAsync().ConfigureAwait(false);
        var missing = RequirePackages.Values
            .Where(spec => !installed.Contains(ExtractPackageName(spec)))
            .ToList();

        if (missing.Count == 0)
        {
            logger.ZLogDebug($"[Pip] Require packages already installed — skipping pip install.");
            return;
        }

        var args = new List<string> { "-m", "pip", "install", "--prefer-binary", "--no-warn-script-location" };
        args.AddRange(missing);

        var result = await Cli.Wrap(PythonExe)
            .WithArguments(args)
            .WithWorkingDirectory(PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => logger.ZLogDebug($"[pip] {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => logger.ZLogDebug($"[pip] {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to verify required packages: {string.Join(", ", missing)}");
    }

    public override async Task InstallPackagesAsync(
        IEnumerable<string> packages,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var requested = packages.ToList();
        if (requested.Count == 0) return;

        var installed = await GetInstalledNamesAsync(cancellationToken).ConfigureAwait(false);
        var missing = requested.Where(spec => !installed.Contains(ExtractPackageName(spec))).ToList();
        if (missing.Count == 0)
        {
            progress.Report("All requested packages already installed.");
            return;
        }

        progress.Report($"Installing {missing.Count} package(s) via pip: {string.Join(", ", missing)}");

        var (succeeded, failed) = await TryPipInstallBatchAsync(
            missing, progress, cancellationToken).ConfigureAwait(false);

        if (succeeded.Count > 0 && failed.Count > 0)
            progress.Report($"pip: {string.Join(", ", succeeded)}");

        if (failed.Count > 0)
        {
            throw new InvalidOperationException(
                $"Failed to install the following package(s): {string.Join(", ", failed)}");
        }

        progress.Report($"All {requested.Count} package(s) processed via pip.");
    }

    /// <inheritdoc />
    public override async Task<string> GetListJsonAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnvironmentReady())
            return string.Empty;

        var stdout = new StringBuilder();
        var result = await Cli.Wrap(PythonExe)
            .WithArguments(["-m", "pip", "list", "--format=json"])
            .WithWorkingDirectory(PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return result.ExitCode == 0 ? stdout.ToString().Trim() : string.Empty;
    }

    private void RemovePthFile(string targetDir)
    {
        var pthFile = Directory.EnumerateFiles(targetDir, "python*._pth").FirstOrDefault();
        if (pthFile is null) return;

        File.Delete(pthFile);
        logger.ZLogDebug($"[Pip] Removed {Path.GetFileName(pthFile)} to enable site-packages.");
    }

    private async Task<bool> IsPipAvailableAsync()
    {
        var result = await Cli.Wrap(PythonExe)
            .WithArguments(["-m", "pip", "--version"])
            .WithWorkingDirectory(PythonHome)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0) return false;

        logger.ZLogDebug($"[Pip] pip already available, skipping bootstrap.");
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

        logger.ZLogInformation($"[Pip] ensurepip unavailable, falling back to get-pip.py...");
        await GetPipAsync().ConfigureAwait(false);
    }

    private async Task<bool> TryEnsurepipAsync()
    {
        logger.ZLogDebug($"[Pip] Trying ensurepip...");

        var result = await Cli.Wrap(PythonExe)
            .WithArguments(["-m", "ensurepip", "--upgrade"])
            .WithWorkingDirectory(PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => logger.ZLogDebug($"[ensurepip] {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => logger.ZLogDebug($"[ensurepip] {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            logger.ZLogDebug($"[Pip] ensurepip failed (exit {result.ExitCode}).");
            return false;
        }

        logger.ZLogInformation($"[Pip] pip bootstrapped via ensurepip.");
        return true;
    }

    private async Task GetPipAsync()
    {
        const string getPipUrl = "https://bootstrap.pypa.io/get-pip.py";
        var getPipPath = Path.Combine(PythonHome, "get-pip.py");

        if (!File.Exists(getPipPath))
        {
            logger.ZLogDebug($"[Pip] Downloading get-pip.py...");
            var script = await NetworkService.GetStringAsync(getPipUrl).ConfigureAwait(false);
            await File.WriteAllTextAsync(getPipPath, script).ConfigureAwait(false);
        }

        var result = await Cli.Wrap(PythonExe)
            .WithArguments([getPipPath, "--no-warn-script-location"])
            .WithWorkingDirectory(PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => logger.ZLogDebug($"[get-pip] {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => logger.ZLogDebug($"[get-pip] {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"get-pip.py failed (exit {result.ExitCode}). " +
                "Cannot bootstrap pip into pyRevit CPython. Check network connectivity.");

        logger.ZLogInformation($"[Pip] pip bootstrapped via get-pip.py.");
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
