using System.IO;
using System.IO.Compression;
using DevTools.Execution.Services;
using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;

// ReSharper disable RedundantSuppressNullableWarningExpression
namespace DevTools.Execution.Providers.Python;

/// <summary>Locked uv.exe under AppData bin.</summary>
public static class UvInstaller
{
    private const string UvVersion = "0.12.8";
    private const string UvDownloadUrlTemplate =
        "https://github.com/astral-sh/uv/releases/download/{0}/uv-x86_64-pc-windows-msvc.zip";

    private static string GetBinPath() => Path.Combine(AppUtils.GetApplicationDataPath(), "bin");
    public static string UvExePath => Path.Combine(GetBinPath(), "uv.exe");
    public static bool IsUvInstalled() => File.Exists(UvExePath) && IsMarkedVersion(UvVersion);
    private static string VersionMarkerPath => Path.Combine(GetBinPath(), ".uv-version");

    public static async Task SetupUvAsync(ILogger? logger = null)
    {
        var outputDir = GetBinPath();
        Directory.CreateDirectory(outputDir);

        if (IsUvInstalled())
        {
#if DEBUG
            logger?.ZLogInformation($"uv {UvVersion} already installed.");
#endif
        }
        else
        {
            logger?.ZLogInformation($"Downloading uv {UvVersion}...");
            await DownloadAndInstallAsync(UvVersion).ConfigureAwait(false);
            await File.WriteAllTextAsync(VersionMarkerPath, UvVersion).ConfigureAwait(false);
            logger?.ZLogInformation($"uv {UvVersion} installed.");
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

    private static async Task DownloadAndInstallAsync(string version)
    {
        var downloadUrl = string.Format(UvDownloadUrlTemplate, version);
        var tempZip = Path.Combine(Path.GetTempPath(), $"uv-{version}.zip");
        var tempExtractDir = Path.Combine(Path.GetTempPath(), $"uv-{version}-extract");

        try
        {
            var zipBytes = await NetworkService.GetBytesAsync(downloadUrl).ConfigureAwait(false);
            await File.WriteAllBytesAsync(tempZip, zipBytes).ConfigureAwait(false);
            if (Directory.Exists(tempExtractDir))
                Directory.Delete(tempExtractDir, true);

            ZipFile.ExtractToDirectory(tempZip, tempExtractDir);

            var uv = Directory.GetFiles(tempExtractDir, "uv.exe", SearchOption.AllDirectories)
                         .FirstOrDefault()
                     ?? throw new FileNotFoundException("uv.exe not found in downloaded archive.");

            var dest = UvExePath;
            if (File.Exists(dest)) File.Delete(dest);
            File.Copy(uv, dest, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempZip)) File.Delete(tempZip);
            if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
        }
    }
}
