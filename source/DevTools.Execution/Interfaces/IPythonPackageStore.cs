using DevTools.Execution.Models;

namespace DevTools.Execution.Interfaces;

/// <summary>Python package operations for one <see cref="PythonBackend"/>.</summary>
public interface IPythonPackageStore
{
    PythonBackend Backend { get; }

    Task<IReadOnlyList<Package>> ListAsync(CancellationToken cancellationToken);

    Task RemoveAsync(Package package, CancellationToken cancellationToken);

    Task UpdateAsync(Package package, CancellationToken cancellationToken);

    Task RepairAsync(Package package, CancellationToken cancellationToken);
}
