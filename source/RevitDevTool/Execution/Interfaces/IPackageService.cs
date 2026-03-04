using RevitDevTool.Execution.Models;

namespace RevitDevTool.Execution.Interfaces;

public interface IPackageService
{
    Task<IReadOnlyList<Package>> ListInstalledPackagesAsync(CancellationToken cancellationToken = default);
    Task RemovePackageAsync(Package package, CancellationToken cancellationToken = default);
    Task RemoveAllAsync(Marketplace marketplace, CancellationToken cancellationToken = default);
    Task UpdateLatestAsync(Package package, CancellationToken cancellationToken = default);
    Task RepairAsync(Package package, CancellationToken cancellationToken = default);
}
