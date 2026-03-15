using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using CliWrap;
using CliWrap.Buffered;
using RevitDevTool.Utils;
// ReSharper disable RedundantSuppressNullableWarningExpression
namespace RevitDevTool.Execution.Providers.FSharp;

internal static class NugetManager
{
    private static readonly string NugetRoot = Path.Combine(SettingsUtils.GetApplicationDataPath(), "nuget");
    private static readonly ConcurrentDictionary<string, string[]> SessionCache = new();

    public static async Task<string[]> ResolvePackageDllsAsync(string packageId, string? version, CancellationToken ct)
    {
        await NugetInstaller.EnsureNugetAsync().ConfigureAwait(false);

        var resolvedVersion = version ?? await FetchLatestVersionAsync(packageId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(resolvedVersion))
            throw new InvalidOperationException($"Could not resolve version for package '{packageId}'.");

        var cacheKey = $"{packageId.ToLowerInvariant()}/{resolvedVersion!.ToLowerInvariant()}";
        if (SessionCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var packageDir = FindInstalledPackageDir(packageId, resolvedVersion);
        if (packageDir == null)
        {
            await InstallPackageAsync(packageId, resolvedVersion, ct).ConfigureAwait(false);
            packageDir = FindInstalledPackageDir(packageId, resolvedVersion)
                ?? throw new InvalidOperationException(
                    $"nuget.exe install succeeded but package folder not found for '{packageId} {resolvedVersion}'.");
        }

        var dlls = ScanDlls(packageDir, packageId, resolvedVersion);
        SessionCache[cacheKey] = dlls;
        return dlls;
    }

    public static async Task<string?> FetchLatestVersionAsync(string packageId, CancellationToken ct)
    {
        await NugetInstaller.EnsureNugetAsync().ConfigureAwait(false);

        // nuget search output format:
        //   > PackageId | Version | Downloads: N
        var result = await Cli.Wrap(NugetInstaller.NugetExePath)
            .WithArguments(["search", packageId, "-Source", "https://api.nuget.org/v3/index.json", "-Take", "1"])
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct).ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"nuget.exe search failed for '{packageId}': {result.StandardError.Trim()}");

        return ParseVersionFromSearchOutput(result.StandardOutput, packageId);
    }

    private static async Task InstallPackageAsync(string packageId, string version, CancellationToken ct)
    {
        Directory.CreateDirectory(NugetRoot);

        var result = await Cli.Wrap(NugetInstaller.NugetExePath)
            .WithArguments([
                "install", packageId,
                "-Version", version,
                "-OutputDirectory", NugetRoot,
                "-Framework", GetCurrentFrameworkMoniker(),
                "-DependencyVersion", "Ignore",
                "-PackageSaveMode", "nuspec;nupkg",
                "-NonInteractive",
                "-Source", "https://api.nuget.org/v3/index.json"
            ])
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct).ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"nuget.exe install failed for '{packageId} {version}': {result.StandardError.Trim()}");

        Trace.TraceInformation($"[NuGetResolver] Installed {packageId} {version}");
    }

    // nuget install creates: <NugetRoot>/<PackageId>.<Version>/lib/<tfm>/*.dll
    private static string? FindInstalledPackageDir(string packageId, string version)
    {
        if (!Directory.Exists(NugetRoot))
            return null;

        var expected = Path.Combine(NugetRoot, $"{packageId}.{version}");
        return Directory.Exists(expected) ? expected : null;
    }

    private static string[] ScanDlls(string packageDir, string packageId, string version)
    {
        var libDir = Path.Combine(packageDir, "lib");
        if (!Directory.Exists(libDir))
        {
            Trace.TraceWarning($"[NuGetResolver] {packageId} {version}: no lib/ folder found.");
            return [];
        }

        foreach (var tfm in BuildTfmPriority())
        {
            var tfmDir = Path.Combine(libDir, tfm);
            if (!Directory.Exists(tfmDir))
                continue;

            var dlls = Directory.GetFiles(tfmDir, "*.dll", SearchOption.TopDirectoryOnly);
            if (dlls.Length <= 0) continue;
            Trace.TraceInformation($"[NuGetResolver] {packageId} {version}: using TFM '{tfm}'");
            return dlls;
        }

        var availableTfms = Directory.GetDirectories(libDir).Select(Path.GetFileName).ToArray();
        throw new InvalidOperationException(
            $"Package '{packageId} {version}' has no compatible TFM for {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}. " +
            $"Available: [{string.Join(", ", availableTfms)}].");
    }

    private static string GetCurrentFrameworkMoniker()
    {
        var ver = Environment.Version;
        return ver.Major == 4 ? "net48" : $"net{ver.Major}.0";
    }

    private static string[] BuildTfmPriority()
    {
        var ver = Environment.Version;

        if (ver.Major == 4)
        {
            return ["net48", "net472", "net471", "net47", "net462", "net461",
                     "net46", "net45", "net40", "net35", "netstandard2.0"];
        }

        var list = new List<string>();
        for (var major = ver.Major; major >= 5; major--)
            list.Add($"net{major}.0");

        list.AddRange(["netcoreapp3.1", "netcoreapp3.0", "netcoreapp2.1",
                        "netstandard2.1", "netstandard2.0"]);

        return list.ToArray();
    }

    // Output: "> Newtonsoft.Json | 13.0.4 | Downloads: 7,829,009,377"
    private static string? ParseVersionFromSearchOutput(string output, string packageId)
    {
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
#if NET
            if (!trimmed.StartsWith('>'))
#else
            if (!trimmed.StartsWith(">", StringComparison.Ordinal))
#endif
                continue;

            // "> PackageId | Version | Downloads: N"
            var parts = trimmed[1..].Split('|');
            if (parts.Length < 2)
                continue;

            var name = parts[0].Trim();
            if (name.Equals(packageId, StringComparison.OrdinalIgnoreCase))
                return parts[1].Trim();
        }

        return null;
    }
}

internal readonly record struct PackageRequest(string PackageId, string? Version);
