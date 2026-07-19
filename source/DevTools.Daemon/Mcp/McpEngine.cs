using DevTools.Daemon.Auth;
using DevTools.Daemon.Hosts;
using DevTools.Daemon.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using DevTools.Daemon.Mcp.Tools;
using DevTools.Mcp.Routing.Catalog;
using DevTools.Mcp.Routing.Broker;
using Microsoft.Extensions.DependencyInjection;

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
        IAuthService authService,
        IOptions<GatewayOptions> gatewayOptions,
        IServiceProvider services)
    {
        InstanceManager = instanceManager;
        BrokerCatalog = brokerCatalog;
        ToolCollection = [];
        PromptCollection = [];
        ResourceCollection = [];

        var broker = new DevToolsBrokerTools(BrokerCatalog, InstanceManager);
        LocalTools = CreateLocalTools(
            authService,
            gatewayOptions,
            services.GetRequiredService<HostDriverRegistry>(),
            broker,
            InstanceManager);
        foreach (var tool in LocalTools)
        {
            ToolCollection.TryAdd(tool);
        }
    }

    public McpServerOptions CreateServerOptions() => ToolHelpers.ConfigureGatewayOptions(
        ToolCollection,
        PromptCollection,
        ResourceCollection);

    private static McpServerTool[] CreateLocalTools(
        IAuthService authService,
        IOptions<GatewayOptions> gatewayOptions,
        HostDriverRegistry hostDrivers,
        DevToolsBrokerTools broker,
        HostSessionManager instanceManager)
    {
        var listMachines = new ListMachinesTool(authService, gatewayOptions);
        var launchHost = new LaunchHostTool(instanceManager, hostDrivers);
        var readFileInfo = new ReadFileInfoTool(hostDrivers);
        var openModel = new OpenModelTool(instanceManager, hostDrivers);

        return
        [
            CreateTool(broker, nameof(DevToolsBrokerTools.Search)),
            CreateTool(broker, nameof(DevToolsBrokerTools.InvokeAsync)),
            CreateTool(listMachines, nameof(ListMachinesTool.ListAsync)),
            CreateTool(launchHost, nameof(LaunchHostTool.LaunchAsync), supportsTasks: true),
            CreateTool(readFileInfo, nameof(ReadFileInfoTool.ReadAsync)),
            CreateTool(openModel, nameof(OpenModelTool.OpenAsync), supportsTasks: true)
        ];
    }

    private static McpServerTool CreateTool<T>(T target, string methodName, bool supportsTasks = false)
        where T : class
    {
        var method = typeof(T).GetMethod(methodName)
            ?? throw new MissingMethodException(typeof(T).FullName, methodName);
        var tool = McpServerTool.Create(method, target);
        if (supportsTasks)
            tool.ProtocolTool.Execution = new ToolExecution { TaskSupport = ToolTaskSupport.Optional };
        return tool;
    }
}
