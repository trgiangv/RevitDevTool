using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Utils;
using DevTools.Mcp.Isolation;
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
        McpAssemblyContextManager assemblyContextManager,
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpServerResourceCollection resources)
    {
        foreach (var source in sources)
            source.ClearCaches();
        assemblyContextManager.Clear();

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
        var byMode = sources.ToDictionary(source => source.ExecMode);

        PopulateTools(catalogStore.RegisteredTools, byMode, hostContext, tools);
        PopulateResources(catalogStore.ResourceCatalog, byMode, hostContext, resources);
    }

    private static void PopulateTools(
        IEnumerable<RegisteredTool> registeredTools,
        IReadOnlyDictionary<ExecutionMode, IMcpSource> sourcesByMode,
        IHostContextExecutor hostContext,
        McpServerPrimitiveCollection<McpServerTool> tools)
    {
        foreach (var tool in registeredTools)
        {
            if (sourcesByMode.TryGetValue(tool.Binding.SourceKind, out var source))
                tools.TryAdd(source.CreateTool(tool, hostContext));
            else
                tools.TryAdd(CatalogToolAdapter.Create(
                    tool.Descriptor,
                    (_, _) => Task.FromResult(UnsupportedTool(tool.Binding.SourceKind))));
        }
    }

    private static void PopulateResources(
        IEnumerable<RegisteredResource> registeredResources,
        IReadOnlyDictionary<ExecutionMode, IMcpSource> sourcesByMode,
        IHostContextExecutor hostContext,
        McpServerResourceCollection resources)
    {
        foreach (var resource in registeredResources)
        {
            if (sourcesByMode.TryGetValue(resource.Binding.SourceKind, out var source))
                resources.TryAdd(source.CreateResource(resource, hostContext));
            else
                resources.TryAdd(CatalogResourceAdapter.Create(
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
