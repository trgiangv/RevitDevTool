namespace RevitDevTool.Mcp.Schemas;

public sealed record Envelope
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ExecutionId { get; set; } = string.Empty;
    public string Version { get; set; } = McpProtocol.Version;
    public string SchemaVersion { get; set; } = McpProtocol.SchemaVersion;
    public string SchemaChecksum { get; set; } = McpProtocol.SchemaChecksum;
    public string Kind { get; set; } = McpMessageKinds.Request;
    public string Action { get; set; } = string.Empty;
    public string? ToolId { get; set; }
    public string? ToolName { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string? Message { get; set; }
    public string? ResultKind { get; set; }
    public McpToolExecutionMetadata? Metadata { get; set; }
    public McpExecutionSnapshot? Execution { get; set; }
    public List<McpProgressUpdate>? ProgressUpdates { get; set; }
    public McpException? Error { get; set; }
}
