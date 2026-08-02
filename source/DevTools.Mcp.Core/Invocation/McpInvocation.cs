using DevTools.Execution.Abstractions;

namespace DevTools.Mcp.Core;

public sealed record McpInvocation
{
    public ExecutionState ExecutionState { get; set; } = ExecutionState.Queued;
}
