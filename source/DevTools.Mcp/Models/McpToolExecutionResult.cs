using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Models;

public sealed record McpToolExecutionResult
{
    private McpToolExecutionResult(
        ExecutionState state,
        string detail,
        CallToolResult result,
        McpErrorInfo? error)
    {
        State = state;
        Detail = detail;
        Result = result;
        Error = error;
    }

    public ExecutionState State { get; }
    public string Detail { get; } = string.Empty;
    public CallToolResult Result { get; } = new();
    public McpErrorInfo? Error { get; }

    public static McpToolExecutionResult Completed(
        CallToolResult result,
        string detail)
        => new(ExecutionState.Completed, detail, result, null);

    public static McpToolExecutionResult Failed(
        string code,
        string message,
        string? details = null)
        => new(ExecutionState.Failed, message, new CallToolResult(),
            new McpErrorInfo { Code = code, Message = message, Details = details });

    public static McpToolExecutionResult Cancelled(string detail)
        => new(ExecutionState.Cancelled, detail, new CallToolResult(),
            new McpErrorInfo { Code = McpExecutionErrorCodes.ToolCancelled, Message = detail });
}
