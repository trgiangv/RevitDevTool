namespace DevTools.Mcp.Routing.Catalog;

public readonly record struct HostCatalogIdentity(string PipeName, int SessionGeneration);

public enum HostCatalogState { Refreshing, Ready, Stale, Unavailable }

public sealed record HostCatalogStatus(
    int HostId,
    HostCatalogState State,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? StaleSince,
    string? LastErrorCode);

public sealed record HostCatalogPublication(
    HostCatalogIdentity Identity,
    HostInstanceDescriptor Instance,
    HostCatalogState State,
    HostCatalogSnapshot? Snapshot,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? StaleSince,
    string? LastErrorCode);
