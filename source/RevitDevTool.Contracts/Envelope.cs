namespace RevitDevTool.Contracts;

public sealed record Envelope
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string ExecutionId { get; init; } = string.Empty;
    public string Version { get; init; } = McpProtocol.Version;
    public string SchemaVersion { get; init; } = McpProtocol.SchemaVersion;
    public string SchemaChecksum { get; init; } = McpProtocol.SchemaChecksum;
    public string Kind { get; init; } = McpMessageKinds.Request;
    public string Action { get; init; } = string.Empty;
    public string? ToolId { get; init; }
    public string? ToolName { get; init; }
    public string PayloadJson { get; init; } = "{}";
    public string? Message { get; init; }
    public string? ResultKind { get; init; }
    public McpToolExecutionMetadata? Metadata { get; init; }
    public McpExecutionSnapshot? Execution { get; init; }
    public List<McpProgressUpdate>? ProgressUpdates { get; init; }
    public McpException? Error { get; init; }
}
