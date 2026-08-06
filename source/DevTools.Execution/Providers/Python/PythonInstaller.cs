using System.IO;
using System.IO.Compression;
using CliWrap;
using DevTools.Execution.Services;
using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;

// ReSharper disable RedundantSuppressNullableWarningExpression
namespace DevTools.Execution.Providers.Python;

/// <summary>
/// Manages Pixi installation by downloading from GitHub releases.
/// Pixi is used as the primary Python environment manager (conda-forge + PyPI via built-in uv).
/// </summary>
public static class PythonInstaller
{
    private const string PixiVersion = "0.76.1";
    private const string PixiDownloadUrlTemplate = "https://github.com/prefix-dev/pixi/releases/download/v{0}/pixi-x86_64-pc-windows-msvc.zip";

    private static string GetBinPath() => Path.Combine(AppUtils.GetApplicationDataPath(), "bin");
    public static string PixiExePath => Path.Combine(GetBinPath(), "pixi.exe");
    public static bool IsPixiInstalled() => File.Exists(PixiExePath) && IsMarkedVersion(PixiVersion);
    private static string VersionMarkerPath => Path.Combine(GetBinPath(), ".pixi-version");

    /// <summary>
    /// Ensures pixi is installed at the locked version and can execute.
    /// Downloads when pixi.exe is missing or the marker version differs from <see cref="PixiVersion"/>.
    /// Runs <c>pixi --version</c> on every call so enterprise blocks on unknown exe trigger pip fallback.
    /// </summary>
    public static async Task SetupPixiAsync(ILogger? logger = null)
    {
        var outputDir = GetBinPath();
        Directory.CreateDirectory(outputDir);

        if (IsPixiInstalled())
        {
            logger?.ZLogInformation($"Pixi v{PixiVersion} already installed.");
        }
        else
        {
            logger?.ZLogInformation($"Downloading v{PixiVersion}...");
            await DownloadAndInstallAsync(PixiVersion).ConfigureAwait(false);
            await File.WriteAllTextAsync(VersionMarkerPath, PixiVersion).ConfigureAwait(false);
            logger?.ZLogInformation($"v{PixiVersion} installed.");
        }

        await VerifyPixiRunnableAsync(logger).ConfigureAwait(false);
    }

    private static async Task VerifyPixiRunnableAsync(ILogger? logger = null)
    {
        var result = await Cli.Wrap(PixiExePath)
            .WithArguments("--version")
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync()
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"pixi --version failed with exit code {result.ExitCode}.");
        }

        logger?.ZLogDebug($"Pixi runtime verified (exit {result.ExitCode}).");
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
