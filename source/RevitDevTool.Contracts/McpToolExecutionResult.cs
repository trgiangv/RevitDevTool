namespace RevitDevTool.Contracts;

public sealed record McpProgressUpdate
{
    public string Stage { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed record McpToolExecutionMetadata
{
    public string ExecutionId { get; init; } = Guid.NewGuid().ToString("N");
    public string ToolId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public long DurationMs { get; init; }
    public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record McpExecutionSnapshot
{
    public string ExecutionId { get; init; } = string.Empty;
    public string ToolId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public string State { get; init; } = McpExecutionStates.Queued;
    public string Message { get; init; } = string.Empty;
    public string ResultKind { get; init; } = McpResultKinds.Empty;
    public bool CanCancel { get; init; }
    public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; init; }
    public McpException? Error { get; init; }
    public IReadOnlyList<McpProgressUpdate> ProgressUpdates { get; init; } = Array.Empty<McpProgressUpdate>();
}

public sealed record McpToolExecutionResult
{
    public bool Success { get; private init; }
    public bool IsCancelled { get; private init; }
    public string Message { get; private init; } = string.Empty;
    public string ResultKind { get; private init; } = McpResultKinds.Json;
    public string PayloadJson { get; private init; } = "{}";
    public McpException? Error { get; private init; }
    public McpToolExecutionMetadata? Metadata { get; private init; }
    public IReadOnlyList<McpProgressUpdate> ProgressUpdates { get; private init; } = Array.Empty<McpProgressUpdate>();

    public static McpToolExecutionResult Succeeded(
        string payloadJson,
        string message,
        string resultKind = McpResultKinds.Json,
        McpToolExecutionMetadata? metadata = null,
        IReadOnlyList<McpProgressUpdate>? progressUpdates = null)
        => new()
        {
            Success = true,
            PayloadJson = payloadJson,
            Message = message,
            ResultKind = resultKind,
            Metadata = metadata,
            ProgressUpdates = progressUpdates ?? Array.Empty<McpProgressUpdate>()
        };

    public static McpToolExecutionResult Failed(
        string code,
        string message,
        string? details = null,
        McpToolExecutionMetadata? metadata = null,
        IReadOnlyList<McpProgressUpdate>? progressUpdates = null)
        => new()
        {
            Success = false,
            Message = message,
            ResultKind = McpResultKinds.Json,
            PayloadJson = "{}",
            Metadata = metadata,
            ProgressUpdates = progressUpdates ?? Array.Empty<McpProgressUpdate>(),
            Error = new McpException { Code = code, Message = message, Details = details }
        };

    public static McpToolExecutionResult Cancelled(
        string message,
        McpToolExecutionMetadata? metadata = null,
        IReadOnlyList<McpProgressUpdate>? progressUpdates = null)
        => new()
        {
            Success = false,
            IsCancelled = true,
            Message = message,
            ResultKind = McpResultKinds.Json,
            PayloadJson = "{}",
            Metadata = metadata,
            ProgressUpdates = progressUpdates ?? [],
            Error = new McpException { Code = "tool.cancelled", Message = message }
        };
}
