using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CliWrap;
using RevitDevTool.Execution.Services;

namespace RevitDevTool.Execution.Providers.Python;

/// <summary>
/// Pip-based Python environment provider for restricted enterprise environments
/// where pixi.exe cannot execute due to security policies.
/// Downloads the official embedded Python distribution from python.org and uses
/// <c>python.exe -m pip</c> for package management.
/// </summary>
public sealed class PipEnvironmentProvider : IPythonEnvironmentProvider
{
    private const string PythonVersion = "3.13.12";
    private const string PythonDownloadUrl = $"https://www.python.org/ftp/python/{PythonVersion}/python-{PythonVersion}-embed-amd64.zip";
    private const string PythonPthFile = "python313._pth";

    public PythonBackend Backend => PythonBackend.Pip;

    public bool IsEnvironmentReady() => File.Exists(PythonEnvironment.PythonExe);

    public string GetPythonDllPath() => PythonEnvironment.GetPythonDllPath();

    public async Task SetupEnvironmentAsync()
    {
        var targetDir = PythonEnvironment.PythonHome;

        if (IsEnvironmentReady())
        {
            Trace.TraceInformation("[Pip] Python environment already exists, skipping download.");
        }
        else
        {
            await DownloadAndExtractAsync(targetDir).ConfigureAwait(false);
            RemovePthFile(targetDir);
            await BootstrapPipAsync().ConfigureAwait(false);
        }

        await EnsureRequirePackagesAsync().ConfigureAwait(false);

        PythonEmbedded.EnsureExtracted();
    }

    /// <summary>
    /// Ensures all <see cref="PythonEnvironment.RequirePackages"/> are installed.
    /// Checks what's already present via <c>pip list</c> and only installs missing ones.
    /// </summary>
    private async Task EnsureRequirePackagesAsync()
    {
        var installed = await RunPipListAsync().ConfigureAwait(false);
        var missing = PythonEnvironment.RequirePackages
            .Where(pkg => !installed.Contains(CanonicalizePackageName(pkg)))
            .ToList();

        if (missing.Count == 0)
        {
            Trace.TraceInformation("[Pip] All required packages already installed.");
            return;
        }

        Trace.TraceInformation($"[Pip] Installing required packages: {string.Join(", ", missing)}");

        var args = new List<string> { "-m", "pip", "install", "--prefer-binary" };
        args.AddRange(missing);

        var result = await Cli.Wrap(PythonEnvironment.PythonExe)
            .WithArguments(args)
            .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Trace.TraceInformation($"[pip] {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Trace.TraceWarning($"[pip] {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new Exception($"Failed to install required packages: {string.Join(", ", missing)}");

        Trace.TraceInformation("[Pip] Required packages installed.");
    }

    public async Task InstallPackagesAsync(
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

    /// <summary>
    /// Returns package names currently installed via pip.
    /// Runs <c>python.exe -m pip list --format=json</c> and extracts canonical names.
    /// </summary>
    private async Task<HashSet<string>> RunPipListAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnvironmentReady()) return [];

        var stdout = new StringBuilder();

        var result = await Cli.Wrap(PythonEnvironment.PythonExe)
            .WithArguments(["-m", "pip", "list", "--format=json"])
            .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
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

    private static async Task DownloadAndExtractAsync(string targetDir)
    {
        Trace.TraceInformation($"[Pip] Downloading Python {PythonVersion} from python.org...");

        var tempZip = Path.Combine(Path.GetTempPath(), $"python-{PythonVersion}-embed.zip");
        var tempExtractDir = Path.Combine(Path.GetTempPath(), $"python-{PythonVersion}-extract");

        try
        {
            var zipBytes = await NetworkService.GetBytesAsync(PythonDownloadUrl).ConfigureAwait(false);
            await File.WriteAllBytesAsync(tempZip, zipBytes).ConfigureAwait(false);

            if (Directory.Exists(tempExtractDir))
                Directory.Delete(tempExtractDir, true);

            ZipFile.ExtractToDirectory(tempZip, tempExtractDir);

            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(tempExtractDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(tempExtractDir.Length).TrimStart(Path.DirectorySeparatorChar);
                var destPath = Path.Combine(targetDir, relativePath);
                var destDir = Path.GetDirectoryName(destPath)!;
                Directory.CreateDirectory(destDir);
                File.Copy(file, destPath, overwrite: true);
            }

            Trace.TraceInformation($"[Pip] Python {PythonVersion} extracted to {targetDir}");
        }
        finally
        {
            if (File.Exists(tempZip)) File.Delete(tempZip);
            if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
        }
    }

    /// <summary>
    /// The ._pth file forces isolated mode which prevents site-packages from loading.
    /// Removing it allows pip and installed packages to work normally.
    /// </summary>
    private static void RemovePthFile(string targetDir)
    {
        var pthPath = Path.Combine(targetDir, PythonPthFile);
        if (!File.Exists(pthPath)) return;

        File.Delete(pthPath);
        Trace.TraceInformation($"[Pip] Removed {PythonPthFile} to enable site-packages.");
    }

    private static async Task BootstrapPipAsync()
    {
        Trace.TraceInformation("[Pip] Bootstrapping pip via ensurepip...");

        var ensurepipResult = await Cli.Wrap(PythonEnvironment.PythonExe)
            .WithArguments(["-m", "ensurepip", "--upgrade"])
            .WithWorkingDirectory(PythonEnvironment.PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Trace.TraceInformation($"[ensurepip] {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Trace.TraceWarning($"[ensurepip] {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (ensurepipResult.ExitCode == 0)
        {
            Trace.TraceInformation("[Pip] pip bootstrapped via ensurepip.");
            return;
        }

        Trace.TraceWarning("[Pip] ensurepip not available in embedded distribution, falling back to get-pip.py...");
        await BootstrapPipViaGetPipAsync().ConfigureAwait(false);
    }

    private static async Task BootstrapPipViaGetPipAsync()
    {
        const string getPipUrl = "https://bootstrap.pypa.io/get-pip.py";
        var getPipPath = Path.Combine(PythonEnvironment.PythonHome, "get-pip.py");

        var script = await NetworkService.GetStringAsync(getPipUrl).ConfigureAwait(false);
        await File.WriteAllTextAsync(getPipPath, script).ConfigureAwait(false);

        var result = await Cli.Wrap(PythonEnvironment.PythonExe)
            .WithArguments(getPipPath)
            .WithWorkingDirectory(PythonEnvironment.PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Trace.TraceInformation($"[get-pip] {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Trace.TraceWarning($"[get-pip] {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new Exception($"get-pip.py failed with exit code {result.ExitCode}. pip is required for pip mode.");

        Trace.TraceInformation("[Pip] pip bootstrapped via get-pip.py.");
    }

    private static async Task<(List<string> Succeeded, List<string> Failed)> TryPipInstallBatchAsync(
        List<string> pkgs,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var args = new List<string> { "-m", "pip", "install", "--prefer-binary" };
        args.AddRange(pkgs);

        var batchResult = await Cli.Wrap(PythonEnvironment.PythonExe)
            .WithArguments(args)
            .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
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

            var singleResult = await Cli.Wrap(PythonEnvironment.PythonExe)
                .WithArguments(["-m", "pip", "install", "--prefer-binary", pkg])
                .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
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
