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

[TestClass]
public sealed class McpServerConfigurationTests
{
    [TestMethod]
    public void AddMcp_ReturnsServicesForChaining()
    {
        var services = new ServiceCollection();

        Assert.AreSame(services, services.AddMcp());
    }

    [TestMethod]
    public void McpServerConfigurator_AppliesRegisteredOptionsBeforeLoggingFilters()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddMcp();
        services.AddSingleton<IConfigureOptions<McpServerOptions>>(new MarkerFilterConfigurer());
        using var provider = services.BuildServiceProvider();
        var options = new McpServerOptions();

        McpServerConfigurator.Apply(options, provider);

        Assert.AreSame(MarkerFilterConfigurer.Filter, options.Filters.Request.CallToolFilters[0]);
        Assert.AreEqual(2, options.Filters.Request.CallToolFilters.Count);
    }

    [TestMethod]
    public void ConfigureDaemonOptions_DisablesExternalListChanged()
    {
        var options = CreateDaemonOptions();

        Assert.IsFalse(options.Capabilities?.Tools?.ListChanged);
        Assert.IsFalse(options.Capabilities?.Prompts?.ListChanged);
        Assert.IsFalse(options.Capabilities?.Resources?.ListChanged);
        Assert.IsNotNull(options.ResourceCollection);
        Assert.IsEmpty(options.ResourceCollection);
        Assert.IsNotEmpty(options.Filters.Request.CallToolFilters);
    }

    [TestMethod]
    public void ConfigureDaemonOptions_AdvertisesTasksExtension()
    {
        var options = CreateDaemonOptions();

        Assert.IsNotNull(options.Capabilities?.Extensions);
        Assert.Contains("io.modelcontextprotocol/tasks", options.Capabilities.Extensions!.Keys);
        Assert.IsNotNull(options.RequestHandlers);
        Assert.Contains(handler => handler.Method == "tasks/get", options.RequestHandlers);
    }

    [TestMethod]
    public void TaskExecutionMeta_MatchesProductPolicy()
    {
        var broker = new Mock<IHostBroker>();
        var invoke = InvokeDynamicTool.Create(broker.Object);
        var search = SearchDynamicTool.Create(broker.Object);
        var optional = TaskModeFixture.CreateOptionalTool("execute_csharp_code");

        Assert.AreEqual(
            McpTaskExecutionMode.Synchronous,
            McpTaskExecutionMeta.SelectForRequest(CreateToolRequest(invoke)));
        Assert.AreEqual(
            McpTaskExecutionMode.Synchronous,
            McpTaskExecutionMeta.SelectForRequest(CreateToolRequest(search)));
        Assert.AreEqual(
            McpTaskExecutionMode.Optional,
            McpTaskExecutionMeta.SelectForRequest(CreateToolRequest(optional)));
        Assert.AreEqual(
            McpTaskExecutionMode.Synchronous,
            McpTaskExecutionMeta.SelectForRequest(CreateToolRequest("unknown_tool")));
        Assert.AreEqual(
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
