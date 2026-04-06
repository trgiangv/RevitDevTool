using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using RevitDevTool.Execution.Services;
using RevitDevTool.Utils;

// ReSharper disable RedundantSuppressNullableWarningExpression
namespace RevitDevTool.Execution.Providers.Python;

/// <summary>
/// Manages Pixi installation by downloading from GitHub releases.
/// Pixi is used as the primary Python environment manager (conda-forge + PyPI via built-in uv).
/// </summary>
public static class PythonInstaller
{
    private const string PixiGitHubApiUrl = "https://api.github.com/repos/prefix-dev/pixi/releases/latest";
    private const string PixiDownloadUrlTemplate = "https://github.com/prefix-dev/pixi/releases/download/v{0}/pixi-x86_64-pc-windows-msvc.zip";

    private static string GetBinPath() => Path.Combine(SettingsUtils.GetApplicationDataPath(), "bin");
    public static string PixiExePath => Path.Combine(GetBinPath(), "pixi.exe");
    public static bool IsPixiInstalled() => File.Exists(PixiExePath);

    /// <summary>
    /// Ensures pixi is installed. If already present, returns immediately.
    /// First-time install requires network access to GitHub releases.
    /// </summary>
    public static async Task SetupPixiAsync()
    {
        var outputDir = GetBinPath();
        Directory.CreateDirectory(outputDir);

        if (IsPixiInstalled())
        {
            Trace.TraceInformation("[Pixi] Already installed, skipping network version check.");
            return;
        }

        var latestVersion = await GetLatestReleaseVersionAsync().ConfigureAwait(false);

        if (string.IsNullOrEmpty(latestVersion))
        {
            throw new InvalidOperationException(
                "Cannot install pixi: failed to determine the latest version from GitHub. " +
                "Ensure network connectivity and retry.");
        }

        Trace.TraceInformation($"Downloading Pixi v{latestVersion}...");
        await DownloadAndInstallAsync(latestVersion!).ConfigureAwait(false);
        Trace.TraceInformation($"Pixi v{latestVersion} installed.");
    }

    private static async Task<string?> GetLatestReleaseVersionAsync()
    {
        try
        {
            var json = await NetworkService.GetStringAsync(PixiGitHubApiUrl).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return !doc.RootElement.TryGetProperty("tag_name", out var tagName) ? null :
                tagName.GetString()?.TrimStart('v');
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to fetch Pixi release info after retries: {ex.Message}");
            return null;
        }
    }

    private static async Task DownloadAndInstallAsync(string version)
    {
        var downloadUrl = string.Format(PixiDownloadUrlTemplate, version);
        var tempZip = Path.Combine(Path.GetTempPath(), $"pixi-{version}.zip");
        var tempExtractDir = Path.Combine(Path.GetTempPath(), $"pixi-{version}-extract");

        try
        {
            var zipBytes = await NetworkService.GetBytesAsync(downloadUrl).ConfigureAwait(false);
            await File.WriteAllBytesAsync(tempZip, zipBytes).ConfigureAwait(false);
            if (Directory.Exists(tempExtractDir))
                Directory.Delete(tempExtractDir, true);

            ZipFile.ExtractToDirectory(tempZip, tempExtractDir);

            var pixi = Directory.GetFiles(tempExtractDir, "pixi.exe", SearchOption.AllDirectories)
                           .FirstOrDefault()
                       ?? throw new FileNotFoundException("pixi.exe not found in downloaded archive.");

            var dest = PixiExePath;
            if (File.Exists(dest)) File.Delete(dest);
            File.Copy(pixi, dest, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempZip)) File.Delete(tempZip);
            if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
        }
    }
}
