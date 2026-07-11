namespace DevTools.Mcp.Routing.Catalog;

public sealed record DynamicResourceCatalogEntry(string Uri, string? Name, string? Description, string? MimeType, InstanceInfo Instance, string PipeName);

public sealed record DynamicResourceResolution(
    DynamicResolutionState State,
    DynamicResourceCatalogEntry? Registration,
    IReadOnlyList<DynamicResourceCatalogEntry> Candidates);
