using System.IO;
using DevTools.Hosting;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.FSharp;
using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Services;

public sealed class NugetPackageStore(
    NugetManager nugetManager,
    IHostAppInfo hostAppInfo,
    ILogger<NugetPackageStore> logger)
{
    private static readonly string CacheRoot = Path.Combine(AppUtils.GetApplicationDataPath(), "nuget");

    public IReadOnlyList<Package> List()
    {
        if (!Directory.Exists(CacheRoot))
            return [];

        var result = new List<Package>();
        foreach (var packageDir in Directory.GetDirectories(CacheRoot))
        {
            var (packageId, version) = ParseFolderName(packageDir);
            if (packageId is null)
                continue;

            result.Add(new Package(Marketplace.NuGet, packageId, version, version));
        }
#if DEBUG
        logger.ZLogDebug($"[PackageService] Found {result.Count} packages in cache.");
#endif
        return result;
    }

    public Task RemoveAsync(Package package, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var folderName = string.IsNullOrWhiteSpace(package.Version)
            ? package.PackageId
            : $"{package.PackageId}.{package.Version}";

        return TryDeleteDirectoryAsync(Path.Combine(CacheRoot, folderName), cancellationToken);
    }

    public async Task RemoveAllAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(CacheRoot))
            return;

        foreach (var dir in Directory.GetDirectories(CacheRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await TryDeleteDirectoryAsync(dir, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task UpdateAsync(Package package, CancellationToken cancellationToken)
        => nugetManager.ResolvePackageDllsAsync(package.PackageId, null, cancellationToken);

    public async Task RepairAsync(Package package, CancellationToken cancellationToken)
    {
        await RemoveAsync(package, cancellationToken).ConfigureAwait(false);
        await nugetManager.ResolvePackageDllsAsync(package.PackageId, package.Version, cancellationToken)
            .ConfigureAwait(false);
    }

    private static (string? PackageId, string? Version) ParseFolderName(string folderPath)
    {
        var folderName = Path.GetFileName(folderPath);
        if (string.IsNullOrWhiteSpace(folderName))
            return (null, null);

        var nuspecFiles = Directory.GetFiles(folderPath, "*.nuspec", SearchOption.TopDirectoryOnly);
        if (nuspecFiles.Length != 1)
            return (null, null);

        var nuspecName = Path.GetFileNameWithoutExtension(nuspecFiles[0]);
        if (folderName.StartsWith(nuspecName + ".", StringComparison.OrdinalIgnoreCase)
            && folderName.Length > nuspecName.Length + 1)
        {
            return (nuspecName, folderName[(nuspecName.Length + 1)..]);
        }

        return (nuspecName, null);
    }

    private async Task TryDeleteDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            await Task.Run(() => Directory.Delete(path, recursive: true), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.ZLogWarning(
                $"[PackageService] Could not delete '{path}': {ex.Message} (files may be locked by {hostAppInfo.Host})");
        }
    }
}
