namespace DevTool.McpParser.Models;

public sealed class InstanceInfo
{
    public int ProcessId { get; init; }
    public string VersionNumber { get; init; } = string.Empty;
    public string? DocumentTitle { get; init; }
    public string? DocumentPath { get; init; }
}
