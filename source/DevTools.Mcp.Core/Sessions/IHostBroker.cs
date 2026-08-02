namespace DevTools.Mcp.Core;

public interface IHostBroker
{
    IConnectedHostCatalog Catalog { get; }
    string MachineId { get; }
    IHostSession? GetByProcessId(int processId);
    IHostSession? GetByHostKey(HostKey key);
    event Action? Changed;
}
