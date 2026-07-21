namespace DevTools.Mcp.Dispatch;

/// <summary>
/// Notifies observers when a standard MCP client session is accepted or ends.
/// Keeps <c>DevTools.Mcp.Hosting</c> free of execution/UI dependencies.
/// </summary>
public interface IMcpSessionLifecycle
{
    void OnSessionAccepted();
    void OnSessionEnded();
}
