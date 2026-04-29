using System.Diagnostics;
using System.IO;
using DevTools.Execution.Services;
using DevTools.Utilities;
namespace DevTools.Execution.Providers.FSharp;

/// <summary>
/// Manages nuget.exe CLI installation. Downloads the latest version from dist.nuget.org.
/// Used by NugetManager for package install and version queries.
/// </summary>
internal static class NugetInstaller
{
    private const string NugetExeDownloadUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe";

    private static string GetBinPath() => Path.Combine(AppUtils.GetApplicationDataPath(), "bin");
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
            await File.WriteAllBytesAsync(tempPath, bytes).ConfigureAwait(false);
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
