namespace DevTools.Execution.External.Connections;

/// <summary>
/// Adapts <see cref="ConnectionState"/> to <see cref="IMcpExecutionTracker"/>
/// so the MCP protocol handler can track execution without a direct dependency
/// on the WPF-bound state type.
/// </summary>
public sealed class ConnectionStateExecutionTracker(ConnectionState state) : IMcpExecutionTracker
{
    public IDisposable BeginExecution(string toolName) => state.BeginExecution(toolName);

    public void MarkRunning(IDisposable scope)
    {
        if (scope is ExecutionScope execScope)
            execScope.MarkRunning();
    }

    public void Complete(IDisposable scope, McpToolExecutionResult result)
    {
        if (scope is ExecutionScope execScope)
            execScope.Complete(result);
    }

    public void RecordCall(string toolId, string toolName) => state.RecordCall(toolId, toolName);
}
