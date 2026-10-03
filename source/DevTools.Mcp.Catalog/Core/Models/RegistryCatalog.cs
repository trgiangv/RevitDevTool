namespace DevTools.Mcp.Core.Models;

public sealed record RegistryCatalog
{
    public IReadOnlyList<RegisteredTool> Tools { get; init; } = [];
    public IReadOnlyList<RegisteredResource> Resources { get; init; } = [];
    public static RegistryCatalog Empty { get; } = new();
    public RegistryCatalog Merge(RegistryCatalog other) => new() { Tools = [.. Tools, .. other.Tools], Resources = [.. Resources, .. other.Resources] };
}
