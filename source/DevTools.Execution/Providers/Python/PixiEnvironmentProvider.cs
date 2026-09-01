using System.IO;
using System.Text;
using CliWrap;
using DevTools.Execution.Models;
using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Providers.Python;

/// <summary>Pixi-owned in-process CPython (conda-forge, then PyPI).</summary>
public sealed class PixiEnvironmentProvider(ILogger<PixiEnvironmentProvider> logger) : PyEnvironmentProvider
{
    private const string PixiEnvDirName = "pixi-env";

    public static readonly string PixiProjectDir =
        Path.Combine(AppUtils.GetApplicationDataPath(), PixiEnvDirName);

    public override PythonBackend Backend => PythonBackend.Pixi;

    protected override string? ManagerExePath => PixiInstaller.PixiExePath;

    protected override Task<string> ResolvePythonHomeAsync()
        => Task.FromResult(Path.Combine(PixiProjectDir, @".pixi\envs\default"));

    public override async Task SetupEnvironmentAsync()
    {
        await PixiInstaller.SetupPixiAsync(logger).ConfigureAwait(false);
        await VerifyRunnableAsync(logger).ConfigureAwait(false);
        await EnsurePythonHomeAsync().ConfigureAwait(false);
        PythonEmbedded.EnsureExtracted();
        await EnsureRequirePackagesAsync().ConfigureAwait(false);

        if (!IsEnvironmentReady())
        {
#if DEBUG
            logger.ZLogDebug($"Running pixi install to bootstrap Python environment...");
#endif
            await RunPixiLoggedOrThrowAsync(PixiArgs.Install(), "pixi install failed.")
                .ConfigureAwait(false);
        }

        if (!IsEnvironmentReady())
            throw new InvalidOperationException("Python environment is not ready after pixi install.");

#if DEBUG
        logger.ZLogDebug($"Pixi Python environment ready.");
#endif
    }

    private async Task EnsureRequirePackagesAsync()
    {
        var installed = await GetInstalledNamesAsync().ConfigureAwait(false);
        var missing = RequirePackages.Values
            .Where(spec => !installed.Contains(ExtractPackageName(spec)))
            .ToList();

        if (missing.Count == 0)
        {
#if DEBUG
            logger.ZLogDebug($"Require packages already installed — skipping pixi add.");
#endif
            return;
        }

        var exit = await RunPixiLoggedAsync(PixiArgs.Add(missing)).ConfigureAwait(false);
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
        if (!PixiInstaller.IsPixiInstalled() || !Directory.Exists(PixiProjectDir))
            return string.Empty;

        var stdout = new StringBuilder();
        var exit = await RunPixiAsync(
                PixiArgs.ListJson(),
                line => stdout.AppendLine(line),
                onStderr: null,
                cancellationToken)
            .ConfigureAwait(false);

        return exit == 0 ? stdout.ToString().Trim() : string.Empty;
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
        var exit = await RunPixiAsync(PixiArgs.Search(packageName), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return exit == 0;
    }

    private static async Task<(List<string> Succeeded, List<string> Failed)> TryAddBatchAsync(
        List<string> pkgs,
        bool pypi,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        if (pkgs.Count == 0)
            return ([], []);

        var batchExit = await RunPixiAsync(
                PixiArgs.Add(pkgs, pypi),
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
                    PixiArgs.Add([pkg], pypi),
                    line => progress.Report($"  {line}"),
                    line => progress.Report($"  {line}"),
                    cancellationToken)
                .ConfigureAwait(false);

            if (exit == 0) succeeded.Add(pkg);
            else failed.Add(pkg);
        }

        return (succeeded, failed);
    }

    private Task<int> RunPixiLoggedAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
        => RunPixiAsync(
            args,
            line => logger.ZLogInformation($"{line}"),
            line => logger.ZLogWarning($"{line}"),
            cancellationToken);

    private async Task RunPixiLoggedOrThrowAsync(
        IReadOnlyList<string> args,
        string failMessage,
        CancellationToken cancellationToken = default)
    {
        var exit = await RunPixiLoggedAsync(args, cancellationToken).ConfigureAwait(false);
        if (exit != 0)
            throw new InvalidOperationException(failMessage);
    }

    internal static async Task<int> RunPixiAsync(
        IReadOnlyList<string> args,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken cancellationToken = default)
    {
        var cmd = Cli.Wrap(PixiInstaller.PixiExePath)
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

    /// <summary><c>pixi.exe</c> argv. <c>add --pypi</c> is PyPI; otherwise conda-forge.</summary>
    internal static class PixiArgs
    {
        public static string[] Install() => ["install"];

        public static string[] ListJson() => ["list", "--json"];

        public static string[] ListExplicitJson() => ["list", "--explicit", "--json"];

        public static string[] Search(string packageName) => ["search", "--limit", "1", packageName];

        public static string[] Update(string packageId) => ["update", packageId];

        public static string[] Add(IEnumerable<string> specs, bool pypi = false)
        {
            var args = new List<string> { "add" };
            if (pypi)
                args.Add("--pypi");
            args.AddRange(specs);
            return [.. args];
        }

        public static string[] Remove(string packageId, bool pypi = false)
        {
            var args = new List<string> { "remove" };
            if (pypi)
                args.Add("--pypi");
            args.Add(packageId);
            return [.. args];
        }
    }
}
