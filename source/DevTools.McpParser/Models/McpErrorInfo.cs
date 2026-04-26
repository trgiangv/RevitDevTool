namespace DevTools.McpParser.Models;

public sealed record McpErrorInfo
{
    public string Code { get; init; } = "bridge.error";
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
}
