using System.IO;
using System.Text;
using CliWrap;
using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Providers.Python;

/// <summary>
/// Pixi-based Python environment provider.
/// Policy: skip when already listed; otherwise search-first conda vs PyPI (not try-first).
/// </summary>
public sealed class PixiEnvironmentProvider(ILogger<PixiEnvironmentProvider> logger) : PyEnvironmentProvider
{
    private const string PixiEnvDirName = "pixi-env";

    public static readonly string PixiProjectDir =
        Path.Combine(AppUtils.GetApplicationDataPath(), PixiEnvDirName);

    public override PythonBackend Backend => PythonBackend.Pixi;

    protected override Task<string> ResolvePythonHomeAsync()
        => Task.FromResult(Path.Combine(PixiProjectDir, @".pixi\envs\default"));

    public override async Task SetupEnvironmentAsync()
    {
        await EnsurePythonHomeAsync().ConfigureAwait(false);
        PythonEmbedded.EnsureExtracted();
        await EnsureRequirePackagesAsync().ConfigureAwait(false);

        if (!IsEnvironmentReady())
        {
            logger.ZLogDebug($"Running pixi install to bootstrap Python environment...");
            var exit = await RunPixiAsync(
                    ["install"],
                    line => logger.ZLogInformation($"{line}"),
                    line => logger.ZLogWarning($"{line}"))
                .ConfigureAwait(false);
            if (exit != 0)
                throw new InvalidOperationException($"pixi install failed with exit code {exit}.");
        }

        if (!IsEnvironmentReady())
            throw new InvalidOperationException("Python environment is not ready after pixi install.");

        logger.ZLogDebug($"Pixi Python environment ready.");
    }

    private async Task EnsureRequirePackagesAsync()
    {
        var installed = await GetInstalledNamesAsync().ConfigureAwait(false);
        var missing = RequirePackages.Values
            .Where(spec => !installed.Contains(ExtractPackageName(spec)))
            .ToList();

        if (missing.Count == 0)
        {
            logger.ZLogDebug($"Require packages already installed — skipping pixi add.");
            return;
        }

        var args = new List<string> { "add" };
        args.AddRange(missing);
        var exit = await RunPixiAsync(args, line => logger.ZLogInformation($"{line}"), line => logger.ZLogWarning($"{line}"))
            .ConfigureAwait(false);

        if (exit != 0)
            logger.ZLogWarning($"Failed to ensure required packages (exit {exit}), will proceed with pixi install.");
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

        var (condaSpecs, pypiSpecs) = await PartitionBySearchAsync(missing, progress, cancellationToken)
            .ConfigureAwait(false);

        if (condaSpecs.Count > 0)
        {
            progress.Report($"Installing from conda: {string.Join(", ", condaSpecs)}");
            var (_, failed) = await TryAddBatchAsync(condaSpecs, pypi: false, progress, cancellationToken)
                .ConfigureAwait(false);
            if (failed.Count > 0)
            {
                progress.Report($"Conda add failed; PyPI fallback for: {string.Join(", ", failed)}");
                pypiSpecs.AddRange(failed);
            }
        }

        if (pypiSpecs.Count > 0)
        {
            progress.Report($"Installing from PyPI: {string.Join(", ", pypiSpecs)}");
            var (_, failed) = await TryAddBatchAsync(pypiSpecs, pypi: true, progress, cancellationToken)
                .ConfigureAwait(false);
            if (failed.Count > 0)
                throw new InvalidOperationException(
                    $"Failed to install the following package(s): {string.Join(", ", failed)}");
        }

        progress.Report($"All {requested.Count} package(s) processed.");
    }

    /// <inheritdoc />
    public override async Task<string> GetListJsonAsync(CancellationToken cancellationToken = default)
    {
        if (!PythonInstaller.IsPixiInstalled() || !Directory.Exists(PixiProjectDir))
            return string.Empty;

        var stdout = new StringBuilder();
        var exit = await Cli.Wrap(PythonInstaller.PixiExePath)
            .WithArguments(["list", "--json"])
            .WithWorkingDirectory(PixiProjectDir)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return exit.ExitCode == 0 ? stdout.ToString().Trim() : string.Empty;
    }

    /// <summary>Split specs by conda availability (testable without pixi.exe).</summary>
    public static (List<string> Conda, List<string> Pypi) PartitionByAvailability(
        IEnumerable<string> specs,
        Func<string, bool> isOnConda)
    {
        var conda = new List<string>();
        var pypi = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in specs)
        {
            var name = ExtractPackageName(spec);
            if (string.IsNullOrEmpty(name) || !seen.Add(name))
                continue;

            if (isOnConda(name))
                conda.Add(spec);
            else
                pypi.Add(spec);
        }

        return (conda, pypi);
    }

    private static async Task<(List<string> Conda, List<string> Pypi)> PartitionBySearchAsync(
        List<string> missing,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var onConda = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in missing)
        {
            var name = ExtractPackageName(spec);
            if (string.IsNullOrEmpty(name) || onConda.ContainsKey(name))
                continue;

            progress.Report($"Searching conda for {name}...");
            onConda[name] = await IsOnCondaAsync(name, cancellationToken).ConfigureAwait(false);
        }

        return PartitionByAvailability(missing, onConda.GetValueOrDefault);
    }

    private static async Task<bool> IsOnCondaAsync(string packageName, CancellationToken cancellationToken)
    {
        // Exit code only — avoid --json (no --limit combo; can dump multi-MB).
        var result = await Cli.Wrap(PythonInstaller.PixiExePath)
            .WithArguments(["search", "--limit", "1", packageName])
            .WithWorkingDirectory(PixiProjectDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return result.ExitCode == 0;
    }

    private static async Task<(List<string> Succeeded, List<string> Failed)> TryAddBatchAsync(
        List<string> pkgs,
        bool pypi,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        if (pkgs.Count == 0)
            return ([], []);

        var args = BuildAddArgs(pkgs, pypi);
        var batchExit = await RunPixiAsync(
                args,
                line => progress.Report($"  {line}"),
                line => progress.Report($"  {line}"),
                cancellationToken)
            .ConfigureAwait(false);

        if (batchExit == 0)
            return (pkgs, []);

        var succeeded = new List<string>();
        var failed = new List<string>();
        foreach (var pkg in pkgs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exit = await RunPixiAsync(
                    BuildAddArgs([pkg], pypi),
                    line => progress.Report($"  {line}"),
                    line => progress.Report($"  {line}"),
                    cancellationToken)
                .ConfigureAwait(false);

            if (exit == 0) succeeded.Add(pkg);
            else failed.Add(pkg);
        }

        return (succeeded, failed);
    }

    private static List<string> BuildAddArgs(IEnumerable<string> pkgs, bool pypi)
    {
        var args = new List<string> { "add" };
        if (pypi) args.Add("--pypi");
        args.AddRange(pkgs);
        return args;
    }

    private static async Task<int> RunPixiAsync(
        IReadOnlyList<string> args,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken cancellationToken = default)
    {
        var cmd = Cli.Wrap(PythonInstaller.PixiExePath)
            .WithArguments(args)
            .WithWorkingDirectory(PixiProjectDir)
            .WithValidation(CommandResultValidation.None);

        if (onStdout is not null)
            cmd = cmd.WithStandardOutputPipe(PipeTarget.ToDelegate(onStdout));
        if (onStderr is not null)
            cmd = cmd.WithStandardErrorPipe(PipeTarget.ToDelegate(onStderr));

        var result = await cmd.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }
}
