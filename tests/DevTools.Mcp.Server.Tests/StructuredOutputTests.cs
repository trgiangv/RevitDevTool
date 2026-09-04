using DevTools.Mcp.Catalog;
using DevTools.Mcp.Client;
using DevTools.Mcp.Core;
using DevTools.Mcp.Server.Tools;
using DevTools.Mcp.Server.Tests.Harness;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Mcp.Server.Tests;

public sealed class StructuredOutputTests
{
    [Fact]
    public async Task SearchDynamic_EmitsStructuredContentWithoutOutputSchema()
    {
        var harness = McpSdkTestHarness.Create();
        var result = await McpToolInvoke.Invoke(harness.SearchTool, "search_dynamic", new { query = "find" });
        var protocolTool = harness.SearchTool.ProtocolTool;

        Assert.NotNull(result.StructuredContent);
        Assert.Null(harness.SearchTool.ProtocolTool.OutputSchema);
        Assert.Contains("\"revit_find_elements\"", result.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
        Assert.Contains("\"revit_find_elements\"", McpToolInvoke.Text(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeDynamic_PreservesHostStructuredContentWithShortText()
    {
        const string toolName = "revit_find_elements";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.StructuredFind);
        var capabilityId = await harness.SearchFirstCapabilityId(new { query = "find" });

        var result = await harness.InvokeCapability(capabilityId, new { category = "Walls" });

        Assert.NotNull(result.StructuredContent);
        Assert.Equal(240, result.StructuredContent!.Value.GetProperty("totalCount").GetInt32());
        Assert.True(result.StructuredContent.Value.GetProperty("hasMore").GetBoolean());
        var text = McpToolInvoke.Text(result);
        Assert.Contains("Found 3 elements", text, StringComparison.Ordinal);
        Assert.True(text.Length < 120, $"Expected compact summary under 120 chars, got {text.Length}: {text}");
        Assert.Equal(1, harness.Session.PassthroughCount);
    }

    [Fact]
    public async Task ListHostInstances_EmitsStructuredContent()
    {
        var broker = new Mock<IHostBroker>();
        broker.Setup(b => b.Catalog.List()).Returns([]);
        var scanner = new Mock<IMcpPipeScanner>();
        scanner.Setup(s => s.Discover()).Returns([]);

        var tool = ListHostInstancesTool.Create(broker.Object, scanner.Object);
        var result = await McpToolInvoke.Invoke(tool, "list_host_instances", new { });

        Assert.NotNull(result.StructuredContent);
        Assert.Null(tool.ProtocolTool.OutputSchema);
        Assert.Equal(0, result.StructuredContent!.Value.GetProperty("totalConnected").GetInt32());
    }

    [Fact]
    public void McpTaskExecutionSelector_UsesPerToolMeta()
    {
        var broker = new Mock<IHostBroker>();
        var invoke = InvokeDynamicTool.Create(broker.Object);
        var search = SearchDynamicTool.Create(broker.Object);
        var optional = TaskModeFixture.CreateOptionalTool("execute_csharp_code");

        Assert.Equal(
            McpTaskExecutionMode.Synchronous,
            McpTaskExecutionMeta.SelectForRequest(McpServerConfigurationTests.CreateToolRequest(invoke)));
        Assert.Equal(
            McpTaskExecutionMode.Synchronous,
            McpTaskExecutionMeta.SelectForRequest(McpServerConfigurationTests.CreateToolRequest(search)));
        Assert.Equal(
            McpTaskExecutionMode.Optional,
            McpTaskExecutionMeta.SelectForRequest(McpServerConfigurationTests.CreateToolRequest(optional)));
        Assert.Equal(
            McpTaskExecutionMode.Synchronous,
            McpTaskExecutionMeta.SelectForRequest(CreateRequestContext("unknown_tool")));
        Assert.Equal(
            McpTaskExecutionMode.Optional,
            McpTaskExecutionMeta.ParseMode(optional.ProtocolTool.Meta));
    }

    private static RequestContext<CallToolRequestParams> CreateRequestContext(string toolName)
    {
        var options = new McpServerOptions();
        var server = new Mock<McpServer>();
        server.Setup(s => s.ServerOptions).Returns(options);
        return new RequestContext<CallToolRequestParams>(
            server.Object,
            new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
            new CallToolRequestParams { Name = toolName });
    }
}
