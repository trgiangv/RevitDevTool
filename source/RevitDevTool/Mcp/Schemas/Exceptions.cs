namespace RevitDevTool.Mcp.Schemas;

public sealed record McpException
{
    public string Code { get; set; } = "bridge.error";
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}
