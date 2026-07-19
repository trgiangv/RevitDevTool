namespace DevTools.Mcp;

public interface IInstanceManager
{
    IReadOnlyCollection<IHostMcpSession> Sessions { get; }
    IHostMcpSession? GetSessionByProcessId(int processId);
    IHostMcpSession? GetSession(int processId, int generation);
    event Action? SessionsChanged;
}
