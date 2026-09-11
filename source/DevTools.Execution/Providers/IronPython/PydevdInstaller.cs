using System.IO;
using System.IO.Compression;
using DevTools.Execution.Services;
using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;

// ReSharper disable RedundantSuppressNullableWarningExpression
namespace DevTools.Execution.Providers.IronPython;

/// <summary>Version-pinned PyDev.Debugger 2.8.0 under AppData (uv-style zip + marker).</summary>
public static class PydevdInstaller
{
    public const string Version = "2.8.0";
    public const string Tag = "pydev_debugger_2_8_0";
    public const string DownloadUrl =
        "https://github.com/fabioz/PyDev.Debugger/archive/refs/tags/pydev_debugger_2_8_0.zip";

    private const string ArchiveFolderName = "PyDev.Debugger-pydev_debugger_2_8_0";
    private static readonly SemaphoreSlim InstallLock = new(1, 1);

    private static string GetPydevdRoot() => Path.Combine(AppUtils.GetApplicationDataPath(), "pydevd");

    public static string ExtractRoot => Path.Combine(GetPydevdRoot(), ArchiveFolderName);

    public static string PydevdPyPath => Path.Combine(ExtractRoot, "pydevd.py");

    internal static string VersionMarkerPath => Path.Combine(GetPydevdRoot(), ".pydevd-version");

    public static bool IsInstalled() => File.Exists(PydevdPyPath) && IsMarkedVersion(Version);

    public static async Task EnsureInstalledAsync(ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        await InstallLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var outputDir = GetPydevdRoot();
            Directory.CreateDirectory(outputDir);

            if (IsInstalled())
            {
#if DEBUG
                logger?.ZLogInformation($"PyDev.Debugger {Version} already installed.");
#endif
                return;
            }

            logger?.ZLogInformation($"Downloading PyDev.Debugger {Version}...");
            await DownloadAndInstallAsync(cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(VersionMarkerPath, Version, cancellationToken).ConfigureAwait(false);
            logger?.ZLogInformation($"PyDev.Debugger {Version} installed.");
        }
        finally
        {
            InstallLock.Release();
        }
    }

    private static bool IsMarkedVersion(string version)
    {
        try
        {
            if (!File.Exists(VersionMarkerPath)) return false;
            var stored = File.ReadAllText(VersionMarkerPath).Trim();
            return string.Equals(stored, version, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static async Task DownloadAndInstallAsync(CancellationToken cancellationToken)
    {
        var tempZip = Path.Combine(Path.GetTempPath(), $"pydevd-{Version}.zip");
        var tempExtractDir = Path.Combine(Path.GetTempPath(), $"pydevd-{Version}-extract");

        try
        {
            var zipBytes = await NetworkService.GetBytesAsync(DownloadUrl, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(tempZip, zipBytes, cancellationToken).ConfigureAwait(false);
            if (Directory.Exists(tempExtractDir))
                Directory.Delete(tempExtractDir, true);

            ZipFile.ExtractToDirectory(tempZip, tempExtractDir);

            var pydevdPy = Directory.GetFiles(tempExtractDir, "pydevd.py", SearchOption.AllDirectories)
                              .FirstOrDefault()
                          ?? throw new FileNotFoundException("pydevd.py not found in downloaded archive.");

            var sourceRoot = Path.GetDirectoryName(pydevdPy)
                             ?? throw new FileNotFoundException("PyDev.Debugger extract root not found.");

            if (Directory.Exists(ExtractRoot))
                Directory.Delete(ExtractRoot, true);

            CopyDirectory(sourceRoot, ExtractRoot);
        }
        finally
        {
            if (File.Exists(tempZip)) File.Delete(tempZip);
            if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
        }
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);

        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }
}
