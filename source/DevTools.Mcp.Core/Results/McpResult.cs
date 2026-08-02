using System.Text.Json.Serialization;

namespace DevTools.Mcp.Core;

public sealed record McpResult<T>
{
    private McpResult(T? value, McpError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public McpError? Error { get; }
    public bool IsSuccess => Error is null;

    public static McpResult<T> Success(T value) => new(value, null);
    public static McpResult<T> Failure(McpError error) => new(default, error);
}

public sealed record McpError(
    string Code,
    string Message,
    IReadOnlyList<ValidationProblem> ValidationProblems,
    string? CorrelationId = null);

public sealed record ValidationProblem(string Property, string Message);

public static class McpErrorCode
{
    public const string ValidationFailed = "validation.failed";
    public const string CapabilityNotFound = "capability.not_found";
    public const string CapabilityAmbiguous = "capability.ambiguous";
    public const string ExecutionCancelled = "execution.cancelled";
    public const string ExecutionFailed = "execution.failed";
    public const string TransportDisconnected = "transport.disconnected";
}

public sealed class McpExecutionException(string mcpErrorCode, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string McpErrorCode { get; } = mcpErrorCode;
}
