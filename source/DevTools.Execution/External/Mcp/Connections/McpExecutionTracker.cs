namespace DevTools.Execution.External.Mcp.Connections;

public interface IMcpExecutionTracker
{
    IDisposable BeginExecution(string toolName);
    void MarkRunning(IDisposable scope);
    void Complete(IDisposable scope, ExecutionState state, string detail);
    void RecordCall(string toolId, string toolName);
}

/// <summary>
/// Adapts <see cref="McpConnectTracker"/> to <see cref="IMcpExecutionTracker"/>
/// so the call filter can track execution without a direct dependency
/// on the WPF-bound state type.
/// </summary>
public sealed class McpExecutionTracker(McpConnectTracker state) : IMcpExecutionTracker
{
    public IDisposable BeginExecution(string toolName) => state.BeginExecution(toolName);

    public void MarkRunning(IDisposable scope)
    {
        if (scope is ExecutionScope execScope)
            execScope.MarkRunning();
    }

    public void Complete(IDisposable scope, ExecutionState state, string detail)
    {
        if (scope is ExecutionScope execScope)
            execScope.Complete(state, detail);
    }

    public void RecordCall(string toolId, string toolName) => state.RecordCall(toolId, toolName);
}
