using System.Collections.Concurrent;
using System.IO;
using CliWrap;
using CliWrap.Buffered;
using DevTools.Execution.Services;
using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;
// ReSharper disable RedundantSuppressNullableWarningExpression
namespace DevTools.Execution.Providers.FSharp;

public sealed class NugetManager(ILogger<NugetManager> logger)
{
    private const string NugetExeDownloadUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe";

    private static readonly string NugetRoot = Path.Combine(AppUtils.GetApplicationDataPath(), "nuget");
    private static readonly string RestoreRoot = Path.Combine(NugetRoot, ".restore");

    private readonly ConcurrentDictionary<string, string[]> _sessionCache = new();

    private static string NugetExePath => Path.Combine(GetBinPath(), "nuget.exe");

    private static string GetBinPath() => Path.Combine(AppUtils.GetApplicationDataPath(), "bin");

    public async Task<string[]> ResolvePackageDllsAsync(string packageId, string? version, CancellationToken ct)
    {
        await EnsureNugetAsync().ConfigureAwait(false);

        var resolvedVersion = version ?? await FetchLatestVersionAsync(packageId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(resolvedVersion))
            throw new InvalidOperationException($"Could not resolve version for package '{packageId}'.");

        var cacheKey = $"{packageId.ToLowerInvariant()}/{resolvedVersion!.ToLowerInvariant()}";
        if (_sessionCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var packageDirs = await RestorePackageGraphAsync(packageId, resolvedVersion, ct).ConfigureAwait(false);
        var dlls = ScanDlls(packageDirs, packageId, resolvedVersion);
        _sessionCache[cacheKey] = dlls;
        return dlls;
    }

    public async Task<string?> FetchLatestVersionAsync(string packageId, CancellationToken ct)
    {
        await EnsureNugetAsync().ConfigureAwait(false);

        return await NetworkService.WithRetryAsync(async () =>
        {
            var result = await Cli.Wrap(NugetExePath)
                .WithArguments(["search", packageId, "-Source", "https://api.nuget.org/v3/index.json", "-Take", "10"])
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(ct).ConfigureAwait(false);

            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"nuget.exe search failed for '{packageId}': {result.StandardError.Trim()}");

            return ParseVersionFromSearchOutput(result.StandardOutput, packageId);
        }).ConfigureAwait(false);
    }

    private async Task EnsureNugetAsync()
    {
        if (File.Exists(NugetExePath))
            return;

        var outputDir = GetBinPath();
        Directory.CreateDirectory(outputDir);

        logger.ZLogInformation($"Downloading nuget.exe...");
        await DownloadNugetExeAsync().ConfigureAwait(false);
        logger.ZLogInformation($"nuget.exe installed.");
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

    private async Task<string[]> RestorePackageGraphAsync(string packageId, string version, CancellationToken ct)
    {
        Directory.CreateDirectory(NugetRoot);
        Directory.CreateDirectory(RestoreRoot);

        var restoreDir = Path.Combine(RestoreRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(restoreDir);

        try
        {
            await InstallPackageGraphAsync(packageId, version, restoreDir, ct).ConfigureAwait(false);

            var restoredPackageDirs = Directory.GetDirectories(restoreDir);
            if (restoredPackageDirs.Length == 0)
            {
                throw new InvalidOperationException(
                    $"nuget.exe install succeeded but no package folders were restored for '{packageId} {version}'.");
            }

            foreach (var restoredPackageDir in restoredPackageDirs)
            {
                var targetDir = Path.Combine(NugetRoot, Path.GetFileName(restoredPackageDir));
                CopyPackageDirectory(restoredPackageDir, targetDir);
            }

            var packageDirs = restoredPackageDirs
                .Select(path => Path.Combine(NugetRoot, Path.GetFileName(path)))
                .Where(Directory.Exists)
                .ToArray();

            if (!packageDirs.Any(IsRequestedPackageDir))
            {
                throw new InvalidOperationException(
                    $"nuget.exe install succeeded but package folder not found for '{packageId} {version}'.");
            }

            return packageDirs;
        }
        finally
        {
            TryDeleteDirectory(restoreDir);
        }

        bool IsRequestedPackageDir(string path) =>
            Path.GetFileName(path).Equals($"{packageId}.{version}", StringComparison.OrdinalIgnoreCase);
    }

    private async Task InstallPackageGraphAsync(string packageId, string version, string outputDirectory, CancellationToken ct)
    {
        await NetworkService.WithRetryAsync(async () =>
        {
            var result = await Cli.Wrap(NugetExePath)
                .WithArguments([
                    "install", packageId,
                    "-Version", version,
                    "-OutputDirectory", outputDirectory,
                    "-Framework", GetCurrentFrameworkMoniker(),
                    "-DependencyVersion", "HighestPatch",
                    "-PackageSaveMode", "nuspec;nupkg",
                    "-NonInteractive",
                    "-Source", "https://api.nuget.org/v3/index.json"
                ])
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(ct).ConfigureAwait(false);

            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"nuget.exe install failed for '{packageId} {version}': {result.StandardError.Trim()}");

            logger.ZLogInformation($"[NuGetResolver] Restored {packageId} {version} dependency graph");
        }).ConfigureAwait(false);
    }

    private static void CopyPackageDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var sourceFile in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = GetRelativePath(sourceDir, sourceFile);
            var targetFile = Path.Combine(targetDir, relativePath);
            if (File.Exists(targetFile))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(sourceFile, targetFile, overwrite: false);
        }
    }

    private static string GetRelativePath(string baseDirectory, string path)
    {
        var baseUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(baseDirectory)));
        var pathUri = new Uri(Path.GetFullPath(path));
        return Uri.UnescapeDataString(baseUri.MakeRelativeUri(pathUri).ToString())
            .Replace('/', Path.DirectorySeparatorChar);
    }

    private static string AppendDirectorySeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
        path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;

    private void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex)
        {
            logger.ZLogWarning($"[NuGetResolver] Failed to delete restore directory '{directory}': {ex.Message}");
        }
    }

    private string[] ScanDlls(IReadOnlyList<string> packageDirs, string packageId, string version)
    {
        var dlls = new List<string>();

        foreach (var packageDir in packageDirs.OrderBy(Path.GetFileName))
            dlls.AddRange(ScanPackageDlls(packageDir));

        if (dlls.Count == 0)
            logger.ZLogWarning($"[NuGetResolver] {packageId} {version}: no compatible DLLs found in restored package graph.");

        return dlls
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string[] ScanPackageDlls(string packageDir)
    {
        var libDir = Path.Combine(packageDir, "lib");
        if (!Directory.Exists(libDir))
        {
            logger.ZLogInformation($"[NuGetResolver] {Path.GetFileName(packageDir)}: no lib/ folder found.");
            return [];
        }

        foreach (var tfm in BuildTfmPriority())
        {
            var tfmDir = Path.Combine(libDir, tfm);
            if (!Directory.Exists(tfmDir))
                continue;

            var dlls = Directory.GetFiles(tfmDir, "*.dll", SearchOption.TopDirectoryOnly);
            if (dlls.Length <= 0) continue;
            logger.ZLogInformation($"[NuGetResolver] {Path.GetFileName(packageDir)}: using TFM '{tfm}'");
            return dlls;
        }

        logger.ZLogInformation(
            $"[NuGetResolver] {Path.GetFileName(packageDir)}: no compatible lib TFM for {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}.");
        return [];
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
            if (!trimmed.StartsWith('>'))
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
