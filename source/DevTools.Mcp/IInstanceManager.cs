namespace DevTools.Mcp;

public interface IInstanceManager
{
    IReadOnlyCollection<InstanceInfo> GetInstances();
    IHostBridgeClient? GetDefault(string? hostApp = null);
    IHostBridgeClient? GetByProcessId(int processId);
    IReadOnlyCollection<string> GetDiscoveredPipeNames();
    event Action? Changed;
}
