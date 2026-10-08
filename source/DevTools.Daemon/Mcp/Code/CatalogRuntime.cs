using System.Collections.Concurrent;
using DevTools.Daemon.Mcp.Processes;

namespace DevTools.Daemon.Mcp.Code;

/// <summary>Catalog and sessions bound into one <see cref="CatalogScript"/> run.</summary>
public sealed class CatalogRuntime(ProcessCatalogs catalog, Func<int, IProcessSession?> session, bool readOnly)
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _gates = new();

    public ProcessCatalogs Catalog { get; } = catalog;

    public Func<int, IProcessSession?> Session { get; } = session;

    public bool ReadOnly { get; } = readOnly;

    public async Task<T> Gate<T>(int processId, Func<Task<T>> call)
    {
        var gate = _gates.GetOrAdd(processId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await call().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
