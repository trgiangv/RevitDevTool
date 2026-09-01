using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using DevTools.Execution.Models;
using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Providers.Python;

/// <summary>uv venv sidecar keyed to host CPython major.minor.</summary>
public sealed class UvEnvironmentProvider(ILogger<UvEnvironmentProvider> logger, Func<string?> hostVersionProbe) : PyEnvironmentProvider
{
    private const string UvEnvDirName = "uv-env";
    private static readonly Regex MajorMinor = new(@"^3\.\d+$", RegexOptions.CultureInvariant);

    public static readonly string UvEnvRoot =
        Path.Combine(AppUtils.GetApplicationDataPath(), UvEnvDirName);

    internal static readonly string UvPythonInstallDir =
        Path.Combine(UvEnvRoot, "uv-python");

    internal static readonly string UvCacheDir =
        Path.Combine(UvEnvRoot, "uv-cache");

    private bool _probed;
    private string? _boundVersion;

    public UvEnvironmentProvider(ILogger<UvEnvironmentProvider> logger)
        : this(logger, PythonNativeEnvironment.TryGetHostPythonVersion) { }

    public override PythonBackend Backend => PythonBackend.Uv;

    protected override string ManagerExePath => UvInstaller.UvExePath;

    internal override void AttachHostInterpreter(string hostDll)
    {
        if (_probed)
            return;

        _probed = true;
        _boundVersion = ResolveBoundVersion(hostDll);
    }

    internal static string? ResolveBoundVersion(string hostDll)
        => PythonNativeEnvironment.ResolveHostVersion(hostDll);

    /// <summary>Host CPython major.minor, e.g. <c>3.13</c>.</summary>
    public string? BoundPythonVersion
    {
        get
        {
            if (_probed) return _boundVersion;
            _probed = true;
            var version = hostVersionProbe()?.Trim();
            _boundVersion = version is { Length: > 0 } && MajorMinor.IsMatch(version) ? version : null;
            return _boundVersion;
        }
    }

    public string BoundEnvDir => BoundPythonVersion is null
        ? string.Empty
        : Path.Combine(UvEnvRoot, BoundPythonVersion);

    public override string PythonExe
    {
        get
        {
            var home = !string.IsNullOrEmpty(PythonHome) ? PythonHome : BoundEnvDir;
            return string.IsNullOrEmpty(home)
                ? string.Empty
                : Path.Combine(home, "Scripts", "python.exe");
        }
    }

    public override bool IsEnvironmentReady()
        => IsVenvRunnable(!string.IsNullOrEmpty(PythonHome) ? PythonHome : BoundEnvDir);

    /// <summary>Trampoline <c>python.exe</c> is not enough — <c>pyvenv.cfg</c> home must still exist.</summary>
    internal static bool IsVenvRunnable(string venvDir)
    {
        if (string.IsNullOrEmpty(venvDir))
            return false;
        if (!File.Exists(Path.Combine(venvDir, "Scripts", "python.exe")))
            return false;
        var cfg = Path.Combine(venvDir, "pyvenv.cfg");
        return TryReadPyvenvHome(cfg, out var prefix)
               && File.Exists(Path.Combine(prefix, "python.exe"));
    }

    protected override Task<string> ResolvePythonHomeAsync()
    {
        if (BoundPythonVersion is null)
        {
            throw new InvalidOperationException(
                "uv sidecar requires a live host CPython interpreter.");
        }

        return Task.FromResult(Path.Combine(UvEnvRoot, BoundPythonVersion));
    }

    public override string GetPythonDllPath()
    {
        if (!Directory.Exists(PythonHome))
            throw new DirectoryNotFoundException($"Python env not found at: {PythonHome}");

        foreach (var dir in new[] { PythonHome, Path.Combine(PythonHome, "Scripts") })
        {
            if (!Directory.Exists(dir))
                continue;

            var dll = Directory.GetFiles(dir, "python3*.dll")
                .FirstOrDefault(f => !PythonNativeEnvironment.IsStableAbiForwarder(f));
            if (dll is not null)
                return dll;
        }

        throw new FileNotFoundException("Python DLL not found in uv venv.", PythonHome);
    }

    public override async Task SetupEnvironmentAsync()
    {
        if (BoundPythonVersion is null)
        {
            throw new InvalidOperationException(
                "uv sidecar requires a live host CPython interpreter.");
        }

        await UvInstaller.SetupUvAsync(logger).ConfigureAwait(false);
        await VerifyRunnableAsync(logger).ConfigureAwait(false);
        await EnsurePythonHomeAsync().ConfigureAwait(false);
        PythonEmbedded.EnsureExtracted();
        Directory.CreateDirectory(UvEnvRoot);
        Directory.CreateDirectory(PythonHome);
        Directory.CreateDirectory(UvPythonInstallDir);
        Directory.CreateDirectory(UvCacheDir);

        await RunUvLoggedOrThrowAsync(
                UvArgs.PythonInstall(BoundPythonVersion),
                $"uv python install {BoundPythonVersion} failed.")
            .ConfigureAwait(false);

        if (!IsEnvironmentReady())
        {
            await RunUvLoggedOrThrowAsync(
                    UvArgs.Venv(BoundPythonVersion, PythonHome),
                    "uv venv failed.")
                .ConfigureAwait(false);
        }

        if (!IsEnvironmentReady())
            throw new InvalidOperationException("Python environment is not ready after uv venv.");

        await EnsureRequirePackagesAsync().ConfigureAwait(false);

#if DEBUG
        logger.ZLogDebug($"uv Python environment ready.");
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
            logger.ZLogDebug($"Require packages already installed — skipping uv pip install.");
#endif
            return;
        }

        var progress = new Progress<string>(line => logger.ZLogInformation($"{line}"));
        var (_, failed) = await TryInstallBatchAsync(missing, progress, CancellationToken.None)
            .ConfigureAwait(false);
        if (failed.Count > 0)
            throw new InvalidOperationException($"Failed to install required packages via uv: {string.Join(", ", failed)}");
    }

    public override async Task InstallPackagesAsync(
        IEnumerable<string> packages,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var requested = packages.ToList();
        if (requested.Count == 0) return;

        if (!IsEnvironmentReady())
            await SetupEnvironmentAsync().ConfigureAwait(false);

        var installed = await GetInstalledNamesAsync(cancellationToken).ConfigureAwait(false);
        var missing = requested.Where(spec => !installed.Contains(ExtractPackageName(spec))).ToList();
        if (missing.Count == 0)
        {
            progress.Report("All requested packages already installed.");
            return;
        }

        progress.Report($"Installing {missing.Count} package(s) via uv: {string.Join(", ", missing)}");
        var (succeeded, failed) = await TryInstallBatchAsync(missing, progress, cancellationToken)
            .ConfigureAwait(false);

        if (succeeded.Count > 0 && failed.Count > 0)
            progress.Report($"uv: {string.Join(", ", succeeded)}");

        if (failed.Count > 0)
        {
            throw new InvalidOperationException(
                $"Failed to install the following package(s): {string.Join(", ", failed)}");
        }

        progress.Report($"All {requested.Count} package(s) processed via uv.");
    }

    public override async Task<string> GetListJsonAsync(CancellationToken cancellationToken = default)
    {
        if (!UvInstaller.IsUvInstalled() || !IsEnvironmentReady())
            return string.Empty;

        var stdout = new StringBuilder();
        var exit = await RunUvAsync(
                UvArgs.PipListJson(PythonExe),
                line => stdout.AppendLine(line),
                onStderr: null,
                cancellationToken)
            .ConfigureAwait(false);

        return exit == 0 ? stdout.ToString().Trim() : string.Empty;
    }

    private async Task<(List<string> Succeeded, List<string> Failed)> TryInstallBatchAsync(
        List<string> packages,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var batchExit = await RunUvAsync(
                UvArgs.PipInstall(PythonExe, packages),
                line => progress.Report($"  {line}"),
                line => progress.Report($"  {line}"),
                cancellationToken)
            .ConfigureAwait(false);

        if (batchExit == 0)
            return (packages, []);

        var succeeded = new List<string>();
        var failed = new List<string>();
        foreach (var pkg in packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exit = await RunUvAsync(
                    UvArgs.PipInstall(PythonExe, [pkg]),
                    line => progress.Report($"  {line}"),
                    line => progress.Report($"  {line}"),
                    cancellationToken)
                .ConfigureAwait(false);

            if (exit == 0) succeeded.Add(pkg);
            else failed.Add(pkg);
        }

        return (succeeded, failed);
    }

    private Task<int> RunUvLoggedAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
        => RunUvAsync(
            args,
            line => logger.ZLogInformation($"{line}"),
            line => logger.ZLogWarning($"{line}"),
            cancellationToken);

    private async Task RunUvLoggedOrThrowAsync(
        IReadOnlyList<string> args,
        string failMessage,
        CancellationToken cancellationToken = default)
    {
        var exit = await RunUvLoggedAsync(args, cancellationToken).ConfigureAwait(false);
        if (exit != 0)
            throw new InvalidOperationException(failMessage);
    }

    internal static async Task<int> RunUvAsync(
        IReadOnlyList<string> args,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken cancellationToken = default)
    {
        var cmd = Cli.Wrap(UvInstaller.UvExePath)
            .WithArguments(args)
            .WithWorkingDirectory(EnsureUvEnvRoot())
            .WithEnvironmentVariables(env =>
            {
                env.Set("UV_PYTHON_INSTALL_DIR", UvPythonInstallDir);
                env.Set("UV_CACHE_DIR", UvCacheDir);
            })
            .WithValidation(CommandResultValidation.None);

        if (onStdout is not null)
            cmd = cmd.WithStandardOutputPipe(PipeTarget.ToDelegate(onStdout));
        if (onStderr is not null)
            cmd = cmd.WithStandardErrorPipe(PipeTarget.ToDelegate(onStderr));

        var result = await cmd.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }

    private static string EnsureUvEnvRoot()
    {
        Directory.CreateDirectory(UvEnvRoot);
        return UvEnvRoot;
    }

    /// <summary><c>uv.exe</c> argv. Package ops are <c>uv pip</c>, not <c>python -m pip</c>.</summary>
    internal static class UvArgs
    {
        public static string[] PythonInstall(string version) => ["python", "install", "--no-bin", version];

        public static string[] Venv(string version, string dest) => ["venv", "--clear", "--python", version, dest];

        public static string[] PipListJson(string pythonExe) => ["pip", "list", "--python", pythonExe, "--format=json"];

        public static string[] PipUninstall(string pythonExe, string packageId)
            => ["pip", "uninstall", "--python", pythonExe, "-y", packageId];

        public static string[] PipInstall(string pythonExe, IEnumerable<string> specs, bool upgrade = false)
        {
            var args = new List<string> { "pip", "install" };
            if (upgrade)
                args.Add("--upgrade");
            args.Add("--python");
            args.Add(pythonExe);
            args.AddRange(specs);
            return [.. args];
        }
    }
}
