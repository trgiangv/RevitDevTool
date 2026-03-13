using ModelContextProtocol.Protocol;

namespace RevitDevTool.Contracts;

public sealed record McpToolExecutionResult
{
    private McpToolExecutionResult(
        ExecutionState state,
        string detail,
        CallToolResult result,
        McpException? error)
    {
        State = state;
        Detail = detail;
        Result = result;
        Error = error;
    }

    public ExecutionState State { get; }
    public string Detail { get; } = string.Empty;
    public CallToolResult Result { get; } = new();
    public McpException? Error { get; }

    public static McpToolExecutionResult Completed(
        CallToolResult result,
        string detail)
        => new(ExecutionState.Completed, detail, result, null);

    public static McpToolExecutionResult Failed(
        string code,
        string message,
        string? details = null)
        => new(ExecutionState.Failed, message, new CallToolResult(),
            new McpException { Code = code, Message = message, Details = details });

    public static McpToolExecutionResult Cancelled(string detail)
        => new(ExecutionState.Cancelled, detail, new CallToolResult(),
            new McpException { Code = ExecutionErrorCodes.ToolCancelled, Message = detail });
}
