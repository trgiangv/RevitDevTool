using DevTools.Daemon.Auth;
using DevTools.Daemon.Hosts;
using DevTools.Daemon.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using DevTools.Daemon.Mcp.Tools;
using DevTools.Mcp.Routing.Catalog;
using DevTools.Mcp.Routing.Broker;

namespace DevTools.Daemon.Mcp;

/// <summary>
/// Pure state container for MCP tool registry and instance management.
/// Lifecycle (discovery, catalog refresh) is owned by hosted services.
/// </summary>
public sealed class McpEngine
{
    public HostSessionManager InstanceManager { get; }
    public BrokerCatalogIndex BrokerCatalog { get; }
    public McpServerPrimitiveCollection<McpServerTool> ToolCollection { get; }
    public McpServerPrimitiveCollection<McpServerPrompt> PromptCollection { get; }
    public McpServerResourceCollection ResourceCollection { get; }
    public IReadOnlyList<McpServerTool> LocalTools { get; }

    public McpEngine(
        HostSessionManager instanceManager,
        BrokerCatalogIndex brokerCatalog,
        HostDriverRegistry hostDrivers,
        IAuthService authService,
        IOptions<GatewayOptions> gatewayOptions)
    {
        InstanceManager = instanceManager;
        BrokerCatalog = brokerCatalog;
        ToolCollection = [];
        PromptCollection = [];
        ResourceCollection = [];

        LocalTools = CreateLocalTools(authService, gatewayOptions, hostDrivers, new DevToolsBrokerTools(BrokerCatalog, InstanceManager));
        foreach (var tool in LocalTools)
        {
            ToolCollection.TryAdd(tool);
        }
    }

    public McpServerOptions CreateServerOptions() => ToolHelpers.ConfigureGatewayOptions(
        ToolCollection,
        PromptCollection,
        ResourceCollection);

    private McpServerTool[] CreateLocalTools(IAuthService authService, IOptions<GatewayOptions> gatewayOptions, HostDriverRegistry hostDrivers, DevToolsBrokerTools broker) =>
    [
        McpServerTool.Create(typeof(DevToolsBrokerTools).GetMethod(nameof(DevToolsBrokerTools.Search))!, broker),
        McpServerTool.Create(typeof(DevToolsBrokerTools).GetMethod(nameof(DevToolsBrokerTools.InvokeAsync))!, broker),
        new ListMachinesTool(authService, gatewayOptions),
        new LaunchHostTool(InstanceManager, hostDrivers),
        new ReadFileInfoTool(hostDrivers),
        new OpenModelTool(InstanceManager, hostDrivers)
    ];
}
