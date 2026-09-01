using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Services;

public sealed class PackageService(
    PythonInitializer pythonInitializer,
    NugetPackageStore nugetStore,
    IEnumerable<IPythonPackageStore> pythonStores,
    PackageVersionChecker packageVersionChecker) : IPackageService
{
    private IPythonPackageStore? PythonStore => 
        pythonInitializer.Provider?.Backend is null 
            ? null : pythonStores.FirstOrDefault(store => store.Backend == pythonInitializer.Provider?.Backend);

    public async Task<IReadOnlyList<Package>> ListInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        var nugetTask = Task.Run(nugetStore.List, cancellationToken);
        var pythonTask = PythonStore?.ListAsync(cancellationToken)
                         ?? Task.FromResult<IReadOnlyList<Package>>([]);

        await Task.WhenAll(nugetTask, pythonTask).ConfigureAwait(false);

        var packages = (await nugetTask.ConfigureAwait(false))
            .Concat(await pythonTask.ConfigureAwait(false))
            .ToList();

        return await packageVersionChecker.AttachLatestVersionsAsync(packages, cancellationToken).ConfigureAwait(false);
    }

    public Task RemovePackageAsync(Package package, CancellationToken cancellationToken = default)
    {
        if (package.IsProtected)
            return Task.CompletedTask;

        return package.Marketplace == Marketplace.NuGet
            ? nugetStore.RemoveAsync(package, cancellationToken)
            : PythonStore?.RemoveAsync(package, cancellationToken) ?? Task.CompletedTask;
    }

    public async Task RemoveAllAsync(Marketplace marketplace, CancellationToken cancellationToken = default)
    {
        if (marketplace == Marketplace.NuGet)
        {
            await nugetStore.RemoveAllAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var all = await ListInstalledPackagesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in all.Where(p => p.Marketplace == marketplace && !p.IsProtected))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RemovePackageAsync(item, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task UpdateLatestAsync(Package package, CancellationToken cancellationToken = default)
    {
        return package.Marketplace == Marketplace.NuGet
            ? nugetStore.UpdateAsync(package, cancellationToken)
            : PythonStore?.UpdateAsync(package, cancellationToken) ?? Task.CompletedTask;
    }

    public Task RepairAsync(Package package, CancellationToken cancellationToken = default)
    {
        return package.Marketplace == Marketplace.NuGet
            ? nugetStore.RepairAsync(package, cancellationToken)
            : PythonStore?.RepairAsync(package, cancellationToken) ?? Task.CompletedTask;
    }
}
