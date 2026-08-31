using DevTools.Mcp.Core.Results;
namespace DevTools.Mcp.Core.Invocation;

public interface IMcpExecutionTracker
{
    IDisposable BeginExecution(string toolName);
    void MarkRunning(IDisposable scope);
    void Complete(IDisposable scope, McpInvocation invocation, McpResult<McpInvocationResponse> result, string detail);
    void RecordCall(string toolId, string toolName);
}
