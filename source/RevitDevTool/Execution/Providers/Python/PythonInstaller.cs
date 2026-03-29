using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using RevitDevTool.Execution.Services;
using RevitDevTool.Utils;

// ReSharper disable RedundantSuppressNullableWarningExpression
namespace RevitDevTool.Execution.Providers.Python;

/// <summary>
/// Manages Pixi installation by downloading the latest version from GitHub releases.
/// Pixi is used as the primary Python environment manager (conda-forge + PyPI via built-in uv).
/// </summary>
// ReSharper disable once PartialTypeWithSinglePart
public static partial class PythonInstaller
{
    private const string PixiGitHubApiUrl = "https://api.github.com/repos/prefix-dev/pixi/releases/latest";
    private const string PixiDownloadUrlTemplate = "https://github.com/prefix-dev/pixi/releases/download/v{0}/pixi-x86_64-pc-windows-msvc.zip";
    private const string VersionPattern = @"pixi\s+(\d+\.\d+\.\d+)";

#if NET
    [GeneratedRegex(VersionPattern)]
    private static partial Regex VersionRegex();
#else
    private static readonly Regex VersionRx = new(VersionPattern, RegexOptions.Compiled);
    private static Regex VersionRegex() => VersionRx;
#endif

    private static string GetBinPath() => Path.Combine(SettingsUtils.GetApplicationDataPath(), "bin");
    public static string PixiExePath => Path.Combine(GetBinPath(), "pixi.exe");
    public static bool IsPixiInstalled() => File.Exists(PixiExePath);

    /// <summary>
    /// Ensures pixi is installed; downloads or updates to latest release if needed.
    /// </summary>
    public static async Task SetupPixiAsync()
    {
        var outputDir = GetBinPath();
        Directory.CreateDirectory(outputDir);

        var currentVersion = await GetCurrentPixiVersionAsync().ConfigureAwait(false);
        var latestVersion  = await GetLatestReleaseVersionAsync().ConfigureAwait(false);

        if (string.IsNullOrEmpty(latestVersion))
        {
            if (!IsPixiInstalled())
            {
                throw new InvalidOperationException(
                    "Cannot install pixi: failed to determine the latest version from GitHub. " +
                    "Ensure network connectivity and retry.");
            }

            Trace.TraceWarning("Could not determine latest Pixi version from GitHub; skipping update check.");
            return;
        }

        if (string.IsNullOrEmpty(currentVersion) || IsNewerVersion(latestVersion!, currentVersion!))
        {
            Trace.TraceInformation($"Downloading Pixi v{latestVersion}...");
            await DownloadAndInstallAsync(latestVersion!).ConfigureAwait(false);
            Trace.TraceInformation($"Pixi v{latestVersion} installed.");
        }
    }

    private static async Task<string?> GetCurrentPixiVersionAsync()
    {
        try
        {
            if (!File.Exists(PixiExePath)) return null;

            var result = await Cli.Wrap(PixiExePath)
                .WithArguments("--version")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync().ConfigureAwait(false);

            if (result.ExitCode != 0) return null;

            var match = VersionRegex().Match(result.StandardOutput.Trim());
            return match.Success ? match.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
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

    private static bool IsNewerVersion(string latest, string current)
    {
        try { return Version.Parse(latest) > Version.Parse(current); }
        catch { return false; }
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
