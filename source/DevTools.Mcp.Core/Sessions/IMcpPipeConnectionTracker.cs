namespace DevTools.Mcp.Core;

/// <summary>
/// Tracks in-host SDK MCP pipe availability and active daemon client sessions for UI binding.
/// Implemented by <c>ConnectionState</c> in <c>DevTools.Execution</c>.
/// </summary>
public interface IMcpPipeConnectionTracker
{
    void SetMcpEndpoint(string endpoint);

    void SetMcpClientCount(int clientCount);

    void ClearMcpState();
}
