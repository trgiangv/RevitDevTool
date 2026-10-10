using System.IO;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
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

    protected override string ManagerExePath => PixiInstaller.PixiExePath;

    protected override Task<string> ResolvePythonHomeAsync()
        => Task.FromResult(Path.Combine(PixiProjectDir, @".pixi\envs\default"));

    public override async Task SetupEnvironmentAsync()
    {
        await PixiInstaller.SetupPixiAsync(logger).ConfigureAwait(false);
        await VerifyRunnableAsync(logger).ConfigureAwait(false);
        await EnsurePythonHomeAsync().ConfigureAwait(false);
        PythonEmbedded.EnsureExtracted(logger);
        var synced = await SyncRequirePackagesAsync().ConfigureAwait(false);

        if (!IsEnvironmentReady() || synced.Count > 0)
        {
#if DEBUG
            logger.ZLogDebug($"Running pixi install to resolve the environment from pixi.toml...");
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

    /// <summary>
    /// <see cref="PyEnvironmentProvider.RequirePackages"/> specs whose <c>requested_spec</c>
    /// from <c>pixi list --explicit --json</c> is not yet that value.
    /// Empty or unreadable JSON syncs every require spec.
    /// </summary>
    internal static List<string> RequireSpecsToSync(
        string explicitListJson,
        IReadOnlyDictionary<string, string> required)
    {
        var listed = ParseRequestedSpecs(explicitListJson);
        var pending = new List<string>();
        foreach (var pair in required)
        {
            if (listed.TryGetValue(pair.Key, out var current) && current == pair.Value)
                continue;

            pending.Add(pair.Key + pair.Value);
        }

        return pending;
    }

    private async Task<List<string>> SyncRequirePackagesAsync()
    {
        var listed = await RunPixiBufferedAsync(PixiArgs.List(explicitOnly: true, noInstall: true)).ConfigureAwait(false);
        var json = listed.ExitCode == 0 ? listed.StandardOutput : string.Empty;
        var pending = RequireSpecsToSync(json, RequirePackages);
        if (pending.Count == 0)
            return pending;

        logger.ZLogInformation($"Syncing RequirePackages: {string.Join(", ", pending)}");
        var exit = await RunPixiLoggedAsync(PixiArgs.Add(pending)).ConfigureAwait(false);
        if (exit != 0)
            logger.ZLogWarning($"pixi add of RequirePackages failed (exit {exit}); pixi install will retry.");

        return pending;
    }

    private static Dictionary<string, string> ParseRequestedSpecs(string json)
    {
        var listed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return listed;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return listed;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("name", out var nameProp) ||
                    nameProp.GetString() is not { Length: > 0 } name)
                    continue;

                listed[name] = item.TryGetProperty("requested_spec", out var specProp) && specProp.ValueKind == JsonValueKind.String
                    ? specProp.GetString() ?? string.Empty
                    : string.Empty;
            }
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return listed;
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

        var onConda = await SearchCondaAsync(missing, progress, cancellationToken).ConfigureAwait(false);
        var (condaSpecs, pypiSpecs) = PartitionByAvailability(missing, onConda.GetValueOrDefault);

        if (condaSpecs.Count > 0)
        {
            progress.Report($"Installing from conda: {string.Join(", ", condaSpecs)}");
            var failed = await TryAddBatchAsync(condaSpecs, pypi: false, progress, cancellationToken)
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
            var failed = await TryAddBatchAsync(pypiSpecs, pypi: true, progress, cancellationToken)
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

        var result = await RunPixiBufferedAsync(PixiArgs.List(), cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : string.Empty;
    }

    private static async Task<Dictionary<string, bool>> SearchCondaAsync(
        List<string> specs,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var onConda = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in specs)
        {
            var name = ExtractPackageName(spec);
            if (string.IsNullOrEmpty(name) || onConda.ContainsKey(name))
                continue;

            progress.Report($"Searching conda for {name}...");
            var exit = await RunPixiAsync(PixiArgs.Search(name), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            onConda[name] = exit == 0;
        }

        return onConda;
    }

    /// <summary>Route each spec to conda or PyPI from a name lookup.</summary>
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

    private static async Task<List<string>> TryAddBatchAsync(
        List<string> packages,
        bool pypi,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        if (packages.Count == 0)
            return [];

        var batchExit = await RunPixiAsync(
                PixiArgs.Add(packages, pypi),
                line => progress.Report($"  {line}"),
                line => progress.Report($"  {line}"),
                cancellationToken)
            .ConfigureAwait(false);

        if (batchExit == 0)
            return [];

        var failed = new List<string>();
        foreach (var pkg in packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exit = await RunPixiAsync(
                    PixiArgs.Add([pkg], pypi),
                    line => progress.Report($"  {line}"),
                    line => progress.Report($"  {line}"),
                    cancellationToken)
                .ConfigureAwait(false);

            if (exit != 0)
                failed.Add(pkg);
        }

        return failed;
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

    internal static async Task<BufferedCommandResult> RunPixiBufferedAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default)
    {
        return await Cli.Wrap(PixiInstaller.PixiExePath)
            .WithArguments(args)
            .WithWorkingDirectory(PixiProjectDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async Task<int> RunPixiAsync(
        IReadOnlyList<string> args,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken cancellationToken = default)
    {
        var result = await RunPixiBufferedAsync(args, cancellationToken).ConfigureAwait(false);
        ReplayLines(result.StandardOutput, onStdout);
        ReplayLines(result.StandardError, onStderr);
        return result.ExitCode;
    }

    /// <summary><c>pixi.exe</c> argv. <c>add --pypi</c> is PyPI; otherwise conda-forge.</summary>
    internal static class PixiArgs
    {
        public static string[] Install() => ["install"];

        public static string[] List(bool explicitOnly = false, bool noInstall = false)
        {
            var args = new List<string> { "list" };
            if (explicitOnly)
                args.Add("--explicit");
            args.Add("--json");
            if (noInstall)
                args.Add("--no-install");
            return [.. args];
        }

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
