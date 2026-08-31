namespace DevTools.Mcp.Core.Results;

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

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record McpError(
    string Code,
    string Message,
    IReadOnlyList<ValidationProblem> ValidationProblems,
    string? CorrelationId = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ValidationProblem(string Property, string Message);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public static class McpErrorCode
{
    public const string ValidationFailed = "validation.failed";
    public const string ExecutionCancelled = "execution.cancelled";
    public const string ExecutionFailed = "execution.failed";
}
