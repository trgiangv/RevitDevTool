using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Routing.Catalog;

public sealed record DynamicPromptCatalogEntry(string Name, string? Description, Prompt ProtocolPrompt, InstanceInfo Instance, string PipeName);

public sealed record DynamicPromptResolution(
    DynamicResolutionState State,
    DynamicPromptCatalogEntry? Registration,
    IReadOnlyList<DynamicPromptCatalogEntry> Candidates);
