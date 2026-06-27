using DevTools.Daemon.Auth;
using DevTools.Daemon.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using DevTools.Daemon.Mcp.Tools;

namespace DevTools.Daemon.Mcp;

/// <summary>
/// Pure state container for MCP tool registry and instance management.
/// Lifecycle (discovery, catalog refresh) is owned by hosted services.
/// </summary>
public sealed class McpEngine
{
    public InstanceManager InstanceManager { get; }
    public DynamicToolCatalog DynamicToolCatalog { get; }
    public McpServerPrimitiveCollection<McpServerTool> ToolCollection { get; }
    public McpServerPrimitiveCollection<McpServerPrompt> PromptCollection { get; }
    public McpServerResourceCollection ResourceCollection { get; }
    public IReadOnlyList<McpServerTool> LocalTools { get; }

    public McpEngine(
        InstanceManager instanceManager,
        DynamicToolCatalog dynamicToolCatalog,
        IAuthService authService,
        IOptions<GatewayOptions> gatewayOptions)
    {
        InstanceManager = instanceManager;
        DynamicToolCatalog = dynamicToolCatalog;
        ToolCollection = [];
        PromptCollection = [];
        ResourceCollection = [];

        LocalTools = CreateLocalTools(authService, gatewayOptions);
        foreach (var tool in LocalTools)
        {
            ToolCollection.TryAdd(tool);
        }
    }

    private McpServerTool[] CreateLocalTools(IAuthService authService, IOptions<GatewayOptions> gatewayOptions) =>
    [
        new ListMachinesTool(authService, gatewayOptions),
        new ListHostInstancesTool(InstanceManager),
        new LaunchHostTool(InstanceManager),
        new ReadFileInfoTool(),
        new OpenModelTool(InstanceManager),
        new ListDynamicTools(DynamicToolCatalog),
        new CallDynamicTool(InstanceManager, DynamicToolCatalog),
        new RefreshDynamicCatalog(DynamicToolCatalog)
    ];
}
