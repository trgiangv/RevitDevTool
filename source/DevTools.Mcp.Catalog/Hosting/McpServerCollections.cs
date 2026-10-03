using DevTools.Execution.Abstractions;
using DevTools.Mcp.Isolation;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Hosting;

/// <summary>Fills the SDK tool and resource collections from the catalog. The first fill is wrapped in <see cref="McpServerOptions"/>.</summary>
public static class McpServerCollections
{
    public static McpServerOptions CreateOptions(
        McpCatalogStore catalogStore,
        IEnumerable<IMcpSource> sources,
        IHostContextExecutor hostContext,
        IServiceProvider appServices)
    {
        var tools = new McpServerPrimitiveCollection<McpServerTool>();
        var resources = new McpServerResourceCollection();
        Populate(catalogStore, sources, hostContext, tools, resources);

        var options = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = "DevTools.Host",
                Version = "1.0.0"
            },
            ToolCollection = tools,
            ResourceCollection = resources,
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability { ListChanged = true },
                Resources = new ResourcesCapability { ListChanged = true, Subscribe = false }
            }
        };

        McpServerConfigurator.Apply(options, appServices);
        return options;
    }

    public static void RebuildCollections(
        McpCatalogStore catalogStore,
        IEnumerable<IMcpSource> sources,
        IHostContextExecutor hostContext,
        McpToolsetContextManager toolsetContextManager,
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpServerResourceCollection resources)
    {
        foreach (var source in sources)
            source.ClearCaches();
        toolsetContextManager.Clear();

        using (tools.DeferChangedEvents())
        using (resources.DeferChangedEvents())
        {
            tools.Clear();
            resources.Clear();
            Populate(catalogStore, sources, hostContext, tools, resources);
        }
    }

    public static void Populate(
        McpCatalogStore catalogStore,
        IEnumerable<IMcpSource> sources,
        IHostContextExecutor hostContext,
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpServerResourceCollection resources)
    {
        catalogStore.EnsureLoaded();
        var byMode = sources.ToDictionary(source => source.SourceKind);

        foreach (var tool in catalogStore.RegisteredTools)
        {
            if (byMode.TryGetValue(tool.Binding.SourceKind, out var source))
                tools.TryAdd(source.CreateTool(tool, hostContext));
            else
                tools.TryAdd(SdkCollectionTool.Create(
                    tool.Descriptor,
                    (_, _) => Task.FromResult(UnsupportedTool(tool.Binding.SourceKind))));
        }

        foreach (var resource in catalogStore.ResourceCatalog)
        {
            if (byMode.TryGetValue(resource.Binding.SourceKind, out var source))
                resources.TryAdd(source.CreateResource(resource, hostContext));
            else
                resources.TryAdd(SdkCollectionResource.Create(
                    resource,
                    hostContext,
                    (_, _) => Task.FromException<ReadResourceResult>(UnsupportedResource(resource.Binding.SourceKind))));
        }
    }

    public static CallToolResult UnsupportedTool(ExecutionMode sourceKind) =>
        ToolHelpers.ErrorResult($"Unsupported MCP tool source '{sourceKind}'.");

    public static InvalidOperationException UnsupportedResource(ExecutionMode sourceKind) =>
        new($"Unsupported MCP resource source '{sourceKind}'.");
}
