namespace RevitDevTool.Contracts;

public sealed record McpException
{
    public string Code { get; init; } = "bridge.error";
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
}
