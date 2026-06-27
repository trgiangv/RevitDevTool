using System.IO;
using CliWrap;
using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Providers.Python;

/// <summary>
/// Pixi-based Python environment provider.
/// Uses conda-forge first, PyPI fallback via pixi's embedded uv.
/// </summary>
public sealed class PixiEnvironmentProvider(ILogger<PixiEnvironmentProvider> logger) : PyEnvironmentProvider
{
    private const string PixiEnvDirName = "pixi-env";

    public static readonly string PixiProjectDir =
        Path.Combine(AppUtils.GetApplicationDataPath(), PixiEnvDirName);

    public override PythonBackend Backend => PythonBackend.Pixi;

    public override bool IsEnvironmentReady()
    {
        EnsurePythonHomeAssigned();
        return File.Exists(PythonExe);
    }

    private void EnsurePythonHomeAssigned()
    {
        PythonHomePath ??= Path.Combine(PixiProjectDir, @".pixi\envs\default");
    }

    public override async Task SetupEnvironmentAsync()
    {
        EnsurePythonHomeAssigned();
        PythonEmbedded.EnsureExtracted();
        await EnsureRequirePackagesAsync().ConfigureAwait(false);

        if (!IsEnvironmentReady())
        {
            logger.ZLogDebug($"Running pixi install to bootstrap Python environment...");

            var result = await Cli.Wrap(PythonInstaller.PixiExePath)
                .WithArguments("install")
                .WithWorkingDirectory(PixiProjectDir)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line => logger.ZLogInformation($"[pixi] {line}")))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line => logger.ZLogWarning($"[pixi] {line}")))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync().ConfigureAwait(false);

            if (result.ExitCode != 0)
                throw new InvalidOperationException($"pixi install failed with exit code {result.ExitCode}.");
        }

        if (!IsEnvironmentReady())
            throw new InvalidOperationException("Python environment is not ready after pixi install.");

        logger.ZLogDebug($"Pixi Python environment ready.");
    }

    private async Task EnsureRequirePackagesAsync()
    {
        var args = new List<string> { "add" };
        args.AddRange(RequirePackages.Values);

        var result = await Cli.Wrap(PythonInstaller.PixiExePath)
            .WithArguments(args)
            .WithWorkingDirectory(PixiProjectDir)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => logger.ZLogInformation($"[pixi] {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => logger.ZLogWarning($"[pixi] {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0)
            logger.ZLogWarning($"[Pixi] Failed to ensure required packages (exit {result.ExitCode}), will proceed with pixi install.");
    }

    public override async Task InstallPackagesAsync(
        IEnumerable<string> packages,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var list = packages.ToList();
        if (list.Count == 0) return;

        var pixi = PythonInstaller.PixiExePath;

        progress.Report($"Trying conda-forge for {list.Count} package(s): {string.Join(", ", list)}");

        var (condaSuccess, condaFailed) = await TryPixiAddBatchAsync(
            pixi, list, pypi: false, progress, cancellationToken).ConfigureAwait(false);

        if (condaFailed.Count == 0)
        {
            progress.Report($"All {list.Count} package(s) installed from conda-forge.");
            return;
        }

        if (condaSuccess.Count > 0)
            progress.Report($"conda-forge: {string.Join(", ", condaSuccess)}");

        progress.Report($"Falling back to PyPI for: {string.Join(", ", condaFailed)}");

        var (pypiSuccess, pypiFailed) = await TryPixiAddBatchAsync(
            pixi, condaFailed, pypi: true, progress, cancellationToken).ConfigureAwait(false);

        if (pypiSuccess.Count > 0)
            progress.Report($"PyPI: {string.Join(", ", pypiSuccess)}");

        if (pypiFailed.Count > 0)
            throw new InvalidOperationException($"Failed to install the following package(s): {string.Join(", ", pypiFailed)}");

        progress.Report($"All {list.Count} package(s) installed.");
    }

    private static async Task<(List<string> Succeeded, List<string> Failed)> TryPixiAddBatchAsync(
        string pixiExe,
        List<string> pkgs,
        bool pypi,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var batchResult = await Cli.Wrap(pixiExe)
            .WithArguments(BuildPixiAddArgs(pkgs, pypi))
            .WithWorkingDirectory(PixiProjectDir)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => progress.Report($"  {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => progress.Report($"  {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);

        if (batchResult.ExitCode == 0)
            return (pkgs, []);

        var succeeded = new List<string>();
        var failed = new List<string>();

        foreach (var pkg in pkgs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var singleResult = await Cli.Wrap(pixiExe)
                .WithArguments(BuildPixiAddArgs([pkg], pypi))
                .WithWorkingDirectory(PixiProjectDir)
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

    private static List<string> BuildPixiAddArgs(IEnumerable<string> pkgs, bool pypi)
    {
        var args = new List<string> { "add" };
        if (pypi) args.Add("--pypi");
        args.AddRange(pkgs);
        return args;
    }
}
