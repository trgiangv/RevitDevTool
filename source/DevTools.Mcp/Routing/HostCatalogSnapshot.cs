using ModelContextProtocol.Client;

namespace DevTools.Mcp.Routing;

public sealed record HostCatalogSnapshot(
    HostInstanceDescriptor Instance,
    IReadOnlyList<McpClientTool> Tools,
    IReadOnlyList<McpClientPrompt> Prompts,
    IReadOnlyList<McpClientResource> Resources,
    IReadOnlyList<McpClientResourceTemplate> ResourceTemplates)
{
    public static HostCatalogSnapshot Create(
        HostInstanceDescriptor instance,
        IList<McpClientTool> tools,
        IList<McpClientPrompt> prompts,
        IList<McpClientResource> resources,
        IList<McpClientResourceTemplate> resourceTemplates) =>
        new(instance, tools.ToArray(), prompts.ToArray(), resources.ToArray(), resourceTemplates.ToArray());
}
