namespace DevTools.McpParser.Models;

public sealed class InstanceInfo
{
    public string? HostApp { get; init; }
    public int ProcessId { get; init; }
    public string VersionNumber { get; init; } = string.Empty;
}
