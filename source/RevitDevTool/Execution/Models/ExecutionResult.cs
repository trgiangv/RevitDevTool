namespace RevitDevTool.Execution.Models;

[PublicAPI]
public sealed class ExecutionResult
{
    public bool Success { get; init; }

    public bool IsCancelled { get; init; }

    public string Message { get; init; } = string.Empty;

    public Exception? Exception { get; init; }

    public long DurationMs { get; init; }

    public static ExecutionResult Succeeded(string message = "", long durationMs = 0)
    {
        return new ExecutionResult
        {
            Success = true,
            Message = message,
            DurationMs = durationMs
        };
    }

    public static ExecutionResult Failed(string message, Exception? exception = null, long durationMs = 0)
    {
        return new ExecutionResult
        {
            Success = false,
            Message = message,
            Exception = exception,
            DurationMs = durationMs
        };
    }

    public static ExecutionResult Cancelled(string message = "Execution cancelled.", long durationMs = 0)
    {
        return new ExecutionResult
        {
            Success = false,
            IsCancelled = true,
            Message = message,
            DurationMs = durationMs
        };
    }

    public static ExecutionResult Skipped(string message = "Node is not executable.")
    {
        return new ExecutionResult
        {
            Success = false,
            Message = message
        };
    }
}
