using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using RevitDevTool.Utils;
// ReSharper disable RedundantSuppressNullableWarningExpression
namespace RevitDevTool.CodeExecute.Providers.Python;

/// <summary>
/// Manages UV installation by downloading the latest version from GitHub releases.
/// </summary>
// ReSharper disable once PartialTypeWithSinglePart
public static partial class UvInstaller
{
    private const string UvGitHubApiUrl = "https://api.github.com/repos/astral-sh/uv/releases/latest";
    private const string UvDownloadUrlTemplate = "https://github.com/astral-sh/uv/releases/download/{0}/uv-x86_64-pc-windows-msvc.zip";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(5) };
    private static readonly string[] UvExecutables = ["uv.exe", "uvx.exe", "uvw.exe"];

#if NETCOREAPP
    [GeneratedRegex(@"uv\s+(\d+\.\d+\.\d+)")]
    private static partial Regex VersionRegex();
#else
    private static Regex VersionRegex() => new(@"uv\s+(\d+\.\d+\.\d+)", RegexOptions.Compiled);
#endif
    
    public static bool IsUvInstalled() => UvExecutables.All(exe => File.Exists(Path.Combine(GetBinPath(), exe)));
    private static string GetBinPath() => Path.Combine(SettingsUtils.GetApplicationDataPath(), "bin");
    
    /// <summary>
    /// Sets up UV: downloads the latest version if not installed or if an update is available.
    /// </summary>
    public static async Task SetupUvAsync()
    {
        var outputDir = GetBinPath();
        Directory.CreateDirectory(outputDir);
        
        var currentVersion = await GetCurrentUvVersionAsync().ConfigureAwait(true);
        var latestVersion = await GetLatestReleaseVersionAsync().ConfigureAwait(true);

        if (string.IsNullOrEmpty(latestVersion))
            return;

        if (string.IsNullOrEmpty(currentVersion) || IsNewerVersion(latestVersion!, currentVersion!))
        {
            await DownloadAndInstallLatestAsync(latestVersion!).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Gets the current installed UV version by executing 'uv --version'.
    /// </summary>
    private static async Task<string?> GetCurrentUvVersionAsync()
    {
        try
        {
            var uvPath = Path.Combine(GetBinPath(), "uv.exe");
            if (!File.Exists(uvPath))
                return null;

            var result = await Cli.Wrap(uvPath)
                .WithArguments("--version")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync().ConfigureAwait(true);

            if (result.ExitCode != 0)
                return null;

            var output = result.StandardOutput.Trim();
            var match = VersionRegex().Match(output);
            return match.Success ? match.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the latest release version from GitHub API.
    /// </summary>
    private static async Task<string?> GetLatestReleaseVersionAsync()
    {
        try
        {
            HttpClient.DefaultRequestHeaders.UserAgent.Clear();
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RevitDevTool");

            var response = await HttpClient.GetStringAsync(UvGitHubApiUrl).ConfigureAwait(true);
            using var document = JsonDocument.Parse(response);

            if (!document.RootElement.TryGetProperty("tag_name", out var tagName)) return null;

            var version = tagName.GetString()?.TrimStart('v'); // (e.g., "v0.10.2" -> "0.10.2")
            return version;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Compares two semantic version strings.
    /// </summary>
    private static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        try
        {
            var latest = Version.Parse(latestVersion);
            var current = Version.Parse(currentVersion);
            return latest > current;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Downloads and installs the latest UV version from GitHub releases.
    /// </summary>
    private static async Task DownloadAndInstallLatestAsync(string version)
    {
        var downloadUrl = string.Format(UvDownloadUrlTemplate, version);
        var tempZipPath = Path.Combine(Path.GetTempPath(), $"uv-{version}.zip");
        var tempExtractPath = Path.Combine(Path.GetTempPath(), $"uv-{version}-extract");

        try
        {
            await DownloadZipAsync(downloadUrl, tempZipPath).ConfigureAwait(true);
            ExtractZip(tempZipPath, tempExtractPath);
            InstallUvExecutable(tempExtractPath);
        }
        finally
        {
            CleanupTempFiles(tempZipPath, tempExtractPath);
        }
    }

    /// <summary>
    /// Downloads the UV zip file from GitHub.
    /// </summary>
    private static async Task DownloadZipAsync(string downloadUrl, string tempZipPath)
    {
        var zipBytes = await HttpClient.GetByteArrayAsync(downloadUrl).ConfigureAwait(true);
#if NET48
        await Task.Run(() => File.WriteAllBytes(tempZipPath, zipBytes)).ConfigureAwait(true);
#else
        await File.WriteAllBytesAsync(tempZipPath, zipBytes).ConfigureAwait(true);
#endif
    }

    /// <summary>
    /// Extracts the UV zip file to a temporary directory.
    /// </summary>
    private static void ExtractZip(string tempZipPath, string tempExtractPath)
    {
        if (Directory.Exists(tempExtractPath))
            Directory.Delete(tempExtractPath, true);
        
        ZipFile.ExtractToDirectory(tempZipPath, tempExtractPath);
    }

    /// <summary>
    /// Installs UV executables (uv.exe, uvx.exe, uv-preview.exe) from the extracted files to the bin directory.
    /// </summary>
    private static void InstallUvExecutable(string tempExtractPath)
    {
        var extractedFiles = Directory.GetFiles(tempExtractPath, "*.exe", SearchOption.AllDirectories);
        var binPath = GetBinPath();

        foreach (var executableName in UvExecutables)
        {
            var sourceFile = extractedFiles.FirstOrDefault(f => 
                Path.GetFileName(f).Equals(executableName, StringComparison.OrdinalIgnoreCase));

            if (sourceFile == null)
                continue;

            var targetPath = Path.Combine(binPath, executableName);
            
            if (File.Exists(targetPath))
                File.Delete(targetPath);
            
            File.Copy(sourceFile, targetPath, true);
        }
    }

    /// <summary>
    /// Cleans up temporary download and extraction files.
    /// </summary>
    private static void CleanupTempFiles(string tempZipPath, string tempExtractPath)
    {
        if (File.Exists(tempZipPath))
            File.Delete(tempZipPath);
        
        if (Directory.Exists(tempExtractPath))
            Directory.Delete(tempExtractPath, true);
    }
}
