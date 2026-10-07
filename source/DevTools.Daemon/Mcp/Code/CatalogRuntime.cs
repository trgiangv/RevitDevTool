using System.Collections.Concurrent;
using DevTools.Daemon.Mcp.Processes;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Mcp.Code;

/// <summary>Catalog and sessions bound into one <see cref="CatalogScript"/> run.</summary>
public sealed class CatalogRuntime
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _gates = new();

    public CatalogRuntime(ProcessCatalogs catalog, Func<int, IProcessSession?> session, bool readOnly)
    {
        Catalog = catalog;
        Session = session;
        ReadOnly = readOnly;
    }

    public ProcessCatalogs Catalog { get; }

    public Func<int, IProcessSession?> Session { get; }

    public bool ReadOnly { get; }

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
