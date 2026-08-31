using DevTools.Ipc;
using ModelContextProtocol.Protocol;
namespace DevTools.Mcp.Core.Sessions;

public enum HostCatalogKind { Tool, Resource, ResourceTemplate }

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class HostCatalogEntry
{
    public required HostKey Key { get; init; }
    public required InstanceInfo Instance { get; init; }
    public required string PipeName { get; init; }
    public required IReadOnlyList<Tool> Tools { get; init; }
    public required IReadOnlyList<Resource> Resources { get; init; }
    public required IReadOnlyList<ResourceTemplate> ResourceTemplates { get; init; }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record HostCatalogHit(HostCatalogKind Kind, string Target, string? Description, HostKey Key, InstanceInfo Instance, Tool? Tool = null, Resource? Resource = null, ResourceTemplate? ResourceTemplate = null);

public enum HostCatalogResolutionState { Found, NotFound, Ambiguous }

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record HostCatalogResolution(HostCatalogResolutionState State, HostCatalogHit? Hit, IReadOnlyList<HostCatalogHit> Candidates);
