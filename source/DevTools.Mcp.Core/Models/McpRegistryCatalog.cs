namespace DevTools.Mcp.Core.Models;

public sealed record McpRegistryCatalog
{
    public IReadOnlyList<McpRegisteredTool> Tools { get; init; } = [];
    public IReadOnlyList<McpRegisteredResource> Resources { get; init; } = [];
    public static McpRegistryCatalog Empty { get; } = new();
    public McpRegistryCatalog Merge(McpRegistryCatalog other) => new() { Tools = [.. Tools, .. other.Tools], Resources = [.. Resources, .. other.Resources] };
}
