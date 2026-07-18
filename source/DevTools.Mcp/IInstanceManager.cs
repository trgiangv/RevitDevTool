namespace DevTools.Mcp;

public interface IInstanceManager
{
    IReadOnlyCollection<IHostMcpSession> Sessions { get; }
    IHostMcpSession? GetSessionByProcessId(int processId);
    event Action? SessionsChanged;

    IReadOnlyCollection<InstanceInfo> GetInstances();
    IHostBridgeClient? GetDefault(string? hostApp = null);
    IHostBridgeClient? GetByProcessId(int processId);
    IReadOnlyCollection<string> GetDiscoveredPipeNames();
    event Action? Changed;
}
