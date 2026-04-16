using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using CliWrap;
using CliWrap.Buffered;
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
    private const string PixiVersion = "0.67.0";
    private const string PixiDownloadUrlTemplate = "https://github.com/prefix-dev/pixi/releases/download/v{0}/pixi-x86_64-pc-windows-msvc.zip";

    private static string GetBinPath() => Path.Combine(SettingsUtils.GetApplicationDataPath(), "bin");
    public static string PixiExePath => Path.Combine(GetBinPath(), "pixi.exe");
    public static bool IsPixiInstalled() => File.Exists(PixiExePath);

    /// <summary>
    /// Ensures pixi is installed at the locked version.
    /// Downloads only when pixi.exe is missing or the local version differs from <see cref="PixiVersion"/>.
    /// </summary>
    public static async Task SetupPixiAsync()
    {
        var outputDir = GetBinPath();
        Directory.CreateDirectory(outputDir);

        if (IsPixiInstalled())
        {
            var localVersion = await GetLocalVersionAsync().ConfigureAwait(false);
            if (string.Equals(localVersion, PixiVersion, StringComparison.Ordinal))
            {
                Trace.TraceInformation($"[Pixi] v{PixiVersion} already installed.");
                return;
            }

            Trace.TraceInformation($"[Pixi] Local version {localVersion ?? "unknown"} differs from locked {PixiVersion}, upgrading...");
        }

        Trace.TraceInformation($"[Pixi] Downloading v{PixiVersion}...");
        await DownloadAndInstallAsync(PixiVersion).ConfigureAwait(false);
        Trace.TraceInformation($"[Pixi] v{PixiVersion} installed.");
    }

    private static async Task<string?> GetLocalVersionAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = await Cli.Wrap(PixiExePath)
                .WithArguments("--version")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cts.Token)
                .ConfigureAwait(false);

            if (result.ExitCode != 0) return null;

            var output = result.StandardOutput.Trim();
            var spaceIndex = output.IndexOf(' ');
            return spaceIndex >= 0 ? output[(spaceIndex + 1)..].Trim() : output;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Pixi] Failed to read local version: {ex.Message}");
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
