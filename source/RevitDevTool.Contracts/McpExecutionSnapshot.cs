namespace RevitDevTool.Contracts;

public sealed record McpExecutionSnapshot
{
    public string ExecutionId { get; init; } = string.Empty;
    public string ToolId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public ExecutionState State { get; init; } = ExecutionState.Queued;
    public string Detail { get; init; } = string.Empty;
    public bool CanCancel { get; init; }
    public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; init; }
    public McpException? Error { get; init; }
}
