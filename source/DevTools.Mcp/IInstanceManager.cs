namespace DevTools.Mcp;

public interface IInstanceManager
{
    IReadOnlyCollection<IHostMcpSession> Sessions { get; }
    IHostMcpSession? GetSessionByProcessId(int processId);
    event Action? SessionsChanged;
}
