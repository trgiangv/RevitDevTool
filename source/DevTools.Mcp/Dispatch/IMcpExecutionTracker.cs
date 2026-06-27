namespace DevTools.Mcp.Dispatch;

/// <summary>
/// Tracks MCP tool execution state (running, completed, failed) and call counts.
/// Abstracts the UI-bound <c>ConnectionState</c> from the handler so the handler
/// can live in the protocol layer without depending on WPF-bound state.
/// </summary>
public interface IMcpExecutionTracker
{
    IDisposable BeginExecution(string toolName);
    void MarkRunning(IDisposable scope);
    void Complete(IDisposable scope, McpToolExecutionResult result);
    void RecordCall(string toolId, string toolName);
}
