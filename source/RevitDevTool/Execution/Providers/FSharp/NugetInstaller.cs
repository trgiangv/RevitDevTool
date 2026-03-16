using System.Diagnostics;
using System.IO;
using RevitDevTool.Execution.Services;
using RevitDevTool.Utils;

namespace RevitDevTool.Execution.Providers.FSharp;

/// <summary>
/// Manages nuget.exe CLI installation. Downloads the latest version from dist.nuget.org.
/// Used by NugetManager for package install and version queries.
/// </summary>
internal static class NugetInstaller
{
    private const string NugetExeDownloadUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe";

    private static string GetBinPath() => Path.Combine(SettingsUtils.GetApplicationDataPath(), "bin");
    public static string NugetExePath => Path.Combine(GetBinPath(), "nuget.exe");

    /// <summary>
    /// Ensures nuget.exe is present. Downloads from dist.nuget.org if missing.
    /// </summary>
    public static async Task EnsureNugetAsync()
    {
        if (File.Exists(NugetExePath))
            return;

        var outputDir = GetBinPath();
        Directory.CreateDirectory(outputDir);

        Trace.TraceInformation("Downloading nuget.exe...");
        await DownloadNugetExeAsync().ConfigureAwait(false);
        Trace.TraceInformation("nuget.exe installed.");
    }

    private static async Task DownloadNugetExeAsync()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"nuget-{Guid.NewGuid():N}.exe");
        try
        {
            var bytes = await NetworkService.GetBytesAsync(NugetExeDownloadUrl).ConfigureAwait(false);
#if NET
            await File.WriteAllBytesAsync(tempPath, bytes).ConfigureAwait(false);
#else
            await Task.Run(() => File.WriteAllBytes(tempPath, bytes)).ConfigureAwait(false);
#endif
            var dest = NugetExePath;
            if (File.Exists(dest)) File.Delete(dest);
            File.Copy(tempPath, dest, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
