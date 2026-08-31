using DevTools.Execution.Abstractions;
namespace DevTools.Mcp.Core.Invocation;

public sealed record McpInvocation
{
    public ExecutionState ExecutionState { get; set; } = ExecutionState.Queued;
}
