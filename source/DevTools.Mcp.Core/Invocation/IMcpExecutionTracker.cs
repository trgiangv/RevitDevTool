namespace DevTools.Mcp.Core;

public interface IMcpExecutionTracker
{
    IDisposable BeginExecution(string toolName);
    void MarkRunning(IDisposable scope);
    void Complete(IDisposable scope, McpInvocation invocation, McpResult<McpInvocationResponse> result, string detail);
    void RecordCall(string toolId, string toolName);
}
