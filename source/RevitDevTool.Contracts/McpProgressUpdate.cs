namespace RevitDevTool.Contracts;

public sealed record McpProgressUpdate
{
    public ExecutionState State { get; init; } = ExecutionState.Preparing;
    public string Detail { get; init; } = string.Empty;
}
