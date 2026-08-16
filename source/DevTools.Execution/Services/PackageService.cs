using System.IO;
using DevTools.Hosting;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Providers.Python;
using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;
namespace DevTools.Execution.Services;

public sealed class PackageService(
    PythonInitializer pythonInitializer,
    IHostAppInfo hostAppInfo,
    NugetManager nugetManager,
    PixiPackageHelper pixiPackageHelper,
    PackageVersionChecker packageVersionChecker,
    ILogger<PackageService> logger) : IPackageService
{
    private static readonly string NuGetCacheRoot = Path.Combine(AppUtils.GetApplicationDataPath(), "nuget");

    private PyEnvironmentProvider? Provider => pythonInitializer.Provider;
    private bool IsPixiBackend => Provider?.Backend == PythonBackend.Pixi;

    public async Task<IReadOnlyList<Package>> ListInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        var nugetTask = Task.Run(ListNuGetPackages, cancellationToken);
        var pythonTask = IsPixiBackend
            ? pixiPackageHelper.ListPackagesAsync(cancellationToken)
            : Provider is not null
                ? PipPackageHelper.ListPackagesAsync(Provider, cancellationToken)
                : Task.FromResult<IReadOnlyList<Package>>([]);

        await Task.WhenAll(nugetTask, pythonTask).ConfigureAwait(false);

        var packages = (await nugetTask.ConfigureAwait(false))
            .Concat(await pythonTask.ConfigureAwait(false))
            .ToList();

        return await packageVersionChecker.AttachLatestVersionsAsync(packages, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemovePackageAsync(Package package, CancellationToken cancellationToken = default)
    {
        if (package.IsProtected)
            return;

        switch (package.Marketplace)
        {
            case Marketplace.NuGet:
                await RemoveNuGetPackageAsync(package, cancellationToken).ConfigureAwait(false);
                break;
            case Marketplace.CondaForge:
                await pixiPackageHelper.RemoveAsync(package.PackageId, pypi: false, cancellationToken).ConfigureAwait(false);
                break;
            case Marketplace.PyPi:
                if (IsPixiBackend)
                    await pixiPackageHelper.RemoveAsync(package.PackageId, pypi: true, cancellationToken).ConfigureAwait(false);
                else if (Provider is not null)
                    await PipPackageHelper.RemoveAsync(Provider, package.PackageId, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    public async Task RemoveAllAsync(Marketplace marketplace, CancellationToken cancellationToken = default)
    {
        switch (marketplace)
        {
            case Marketplace.NuGet:
                await RemoveAllNuGetAsync(cancellationToken).ConfigureAwait(false);
                break;
            case Marketplace.CondaForge:
            case Marketplace.PyPi:
                var all = await ListInstalledPackagesAsync(cancellationToken).ConfigureAwait(false);
                var targets = all
                    .Where(item => item.Marketplace == marketplace && !item.IsProtected)
                    .ToArray();
                foreach (var item in targets)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await RemovePackageAsync(item, cancellationToken).ConfigureAwait(false);
                }
                break;
        }
    }

    public Task UpdateLatestAsync(Package package, CancellationToken cancellationToken = default)
    {
        return package.Marketplace switch
        {
            Marketplace.NuGet => nugetManager.ResolvePackageDllsAsync(package.PackageId, null, cancellationToken),
            Marketplace.CondaForge when IsPixiBackend => pixiPackageHelper.UpdateAsync(package, pypi: false, cancellationToken),
            Marketplace.PyPi when IsPixiBackend => pixiPackageHelper.UpdateAsync(package, pypi: true, cancellationToken),
            Marketplace.PyPi when Provider is not null => PipPackageHelper.UpdateAsync(Provider, package, cancellationToken),
            _ => Task.CompletedTask
        };
    }

    public async Task RepairAsync(Package package, CancellationToken cancellationToken = default)
    {
        if (package.Marketplace == Marketplace.NuGet)
        {
            await RemoveNuGetPackageAsync(package, cancellationToken).ConfigureAwait(false);
            await nugetManager.ResolvePackageDllsAsync(package.PackageId, package.Version, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (IsPixiBackend)
        {
            var isPypi = package.Marketplace == Marketplace.PyPi;
            await pixiPackageHelper.RemoveAsync(package.PackageId, isPypi, cancellationToken).ConfigureAwait(false);
            await pixiPackageHelper.InstallAsync(package.PackageId, package.DeclaredVersion, isPypi, cancellationToken).ConfigureAwait(false);
        }
        else if (Provider is not null)
        {
            await PipPackageHelper.RemoveAsync(Provider, package.PackageId, cancellationToken).ConfigureAwait(false);
            await PipPackageHelper.InstallAsync(Provider, package.PackageId, package.DeclaredVersion, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IEnumerable<Package> ListNuGetPackages()
    {
        if (!Directory.Exists(NuGetCacheRoot))
            return [];

        var result = new List<Package>();
        foreach (var packageDir in Directory.GetDirectories(NuGetCacheRoot))
        {
            var (packageId, version) = ParseNuGetFolderName(packageDir);
            if (packageId == null)
                continue;

            result.Add(new Package(Marketplace.NuGet, packageId, version, version));
        }
        return result;
    }

    private static (string? PackageId, string? Version) ParseNuGetFolderName(string folderPath)
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

    private Task RemoveNuGetPackageAsync(Package package, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var folderName = string.IsNullOrWhiteSpace(package.Version)
            ? package.PackageId
            : $"{package.PackageId}.{package.Version}";

        var packageDir = Path.Combine(NuGetCacheRoot, folderName);
        return TryDeleteDirectoryAsync(packageDir, cancellationToken);
    }

    private async Task RemoveAllNuGetAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(NuGetCacheRoot))
            return;

        foreach (var dir in Directory.GetDirectories(NuGetCacheRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await TryDeleteDirectoryAsync(dir, cancellationToken).ConfigureAwait(false);
        }
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
            logger.ZLogWarning($"[PackageService] Could not delete '{path}': {ex.Message} (files may be locked by {hostAppInfo.Host})");
        }
    }
}
