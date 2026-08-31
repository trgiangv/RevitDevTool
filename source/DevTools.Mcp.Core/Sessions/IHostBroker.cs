namespace DevTools.Mcp.Core.Sessions;

public interface IHostBroker
{
    IConnectedHostCatalog Catalog { get; }
    IHostSession? GetByProcessId(int processId);
    IHostSession? GetByHostKey(HostKey key);
    event Action? Changed;
}
