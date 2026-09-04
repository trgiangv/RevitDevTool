using DevTools.Mcp.Server.Hosting;
using DevTools.Mcp.Server.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Mcp.Server.Tests;

public sealed class McpServerConfigurationTests
{
    [Fact]
    public void AddMcp_ReturnsServicesForChaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddMcp());
    }

    [Fact]
    public void McpServerConfigurator_AppliesRegisteredOptionsBeforeLoggingFilters()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddMcp();
        services.AddSingleton<IConfigureOptions<McpServerOptions>>(new MarkerFilterConfigurer());
        using var provider = services.BuildServiceProvider();
        var options = new McpServerOptions();

        McpServerConfigurator.Apply(options, provider);

        Assert.Same(MarkerFilterConfigurer.Filter, options.Filters.Request.CallToolFilters[0]);
        Assert.Equal(2, options.Filters.Request.CallToolFilters.Count);
    }

    [Fact]
    public void ConfigureDaemonOptions_DisablesExternalListChanged()
    {
        var options = CreateDaemonOptions();

        Assert.False(options.Capabilities?.Tools?.ListChanged);
        Assert.False(options.Capabilities?.Prompts?.ListChanged);
        Assert.False(options.Capabilities?.Resources?.ListChanged);
        Assert.NotNull(options.ResourceCollection);
        Assert.Empty(options.ResourceCollection);
        Assert.NotEmpty(options.Filters.Request.CallToolFilters);
    }

    [Fact]
    public void ConfigureDaemonOptions_AdvertisesTasksExtension()
    {
        var options = CreateDaemonOptions();

        Assert.NotNull(options.Capabilities?.Extensions);
        Assert.Contains("io.modelcontextprotocol/tasks", options.Capabilities.Extensions!.Keys);
        Assert.NotNull(options.RequestHandlers);
        Assert.Contains(options.RequestHandlers, handler => handler.Method == "tasks/get");
    }

    [Fact]
    public void TaskExecutionMeta_MatchesProductPolicy()
    {
        var broker = new Mock<IHostBroker>();
        var invoke = InvokeDynamicTool.Create(broker.Object);
        var search = SearchDynamicTool.Create(broker.Object);
        var optional = TaskModeFixture.CreateOptionalTool("execute_csharp_code");

        Assert.Equal(
            McpTaskExecutionMode.Synchronous,
            McpTaskExecutionMeta.SelectForRequest(CreateToolRequest(invoke)));
        Assert.Equal(
            McpTaskExecutionMode.Synchronous,
            McpTaskExecutionMeta.SelectForRequest(CreateToolRequest(search)));
        Assert.Equal(
            McpTaskExecutionMode.Optional,
            McpTaskExecutionMeta.SelectForRequest(CreateToolRequest(optional)));
        Assert.Equal(
            McpTaskExecutionMode.Synchronous,
            McpTaskExecutionMeta.SelectForRequest(CreateToolRequest("unknown_tool")));
        Assert.Equal(
            McpTaskExecutionMode.Optional,
            McpTaskExecutionMeta.ParseMode(optional.ProtocolTool.Meta));
    }

    private static RequestContext<CallToolRequestParams> CreateToolRequest(string toolName)
    {
        var options = new McpServerOptions();
        var server = new Mock<McpServer>();
        server.Setup(s => s.ServerOptions).Returns(options);
        return new RequestContext<CallToolRequestParams>(
            server.Object,
            new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
            new CallToolRequestParams { Name = toolName });
    }

    internal static McpServerOptions CreateDaemonOptions() =>
        McpServerFactory.CreateOptions([], [], TestMcpAppServices.Create());

    internal static RequestContext<CallToolRequestParams> CreateToolRequest(McpServerTool tool)
    {
        var collection = new McpServerPrimitiveCollection<McpServerTool>();
        collection.TryAdd(tool);
        var options = new McpServerOptions { ToolCollection = collection };
        var server = new Mock<McpServer>();
        server.Setup(s => s.ServerOptions).Returns(options);
        return new RequestContext<CallToolRequestParams>(
            server.Object,
            new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
            new CallToolRequestParams { Name = tool.ProtocolTool.Name });
    }

    private sealed class MarkerFilterConfigurer : IConfigureOptions<McpServerOptions>
    {
        public static readonly McpRequestFilter<CallToolRequestParams, CallToolResult> Filter = next => next;

        public void Configure(McpServerOptions options) =>
            options.Filters.Request.CallToolFilters.Add(Filter);
    }
}
