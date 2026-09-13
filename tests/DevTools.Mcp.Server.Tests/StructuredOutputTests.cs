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

[TestClass]
public sealed class StructuredOutputTests
{
    [TestMethod]
    public async Task SearchDynamic_EmitsStructuredContentWithoutOutputSchema()
    {
        var harness = McpSdkTestHarness.Create();
        var result = await McpToolInvoke.Invoke(harness.SearchTool, "search_dynamic", new { query = "find" });
        var protocolTool = harness.SearchTool.ProtocolTool;

        Assert.IsNotNull(result.StructuredContent);
        Assert.IsNull(harness.SearchTool.ProtocolTool.OutputSchema);
        Assert.Contains("\"revit_find_elements\"", result.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
        Assert.Contains("\"revit_find_elements\"", McpToolInvoke.Text(result), StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task InvokeDynamic_PreservesHostStructuredContentWithShortText()
    {
        const string toolName = "revit_find_elements";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.StructuredFind);
        var capabilityId = await harness.SearchFirstCapabilityId(new { query = "find" });

        var result = await harness.InvokeCapability(capabilityId, new { category = "Walls" });

        Assert.IsNotNull(result.StructuredContent);
        Assert.AreEqual(240, result.StructuredContent!.Value.GetProperty("totalCount").GetInt32());
        Assert.IsTrue(result.StructuredContent.Value.GetProperty("hasMore").GetBoolean());
        var text = McpToolInvoke.Text(result);
        Assert.Contains("Found 3 elements", text, StringComparison.Ordinal);
        Assert.IsTrue(text.Length < 120, $"Expected compact summary under 120 chars, got {text.Length}: {text}");
        Assert.AreEqual(1, harness.Session.PassthroughCount);
    }

    [TestMethod]
    public async Task ListHostInstances_EmitsStructuredContent()
    {
        var broker = new Mock<IHostBroker>();
        broker.Setup(b => b.Catalog.List()).Returns([]);
        var scanner = new Mock<IMcpPipeScanner>();
        scanner.Setup(s => s.Discover()).Returns([]);

        var tool = ListHostInstancesTool.Create(broker.Object, scanner.Object);
        var result = await McpToolInvoke.Invoke(tool, "list_host_instances", new { });

        Assert.IsNotNull(result.StructuredContent);
        Assert.IsNull(tool.ProtocolTool.OutputSchema);
        Assert.AreEqual(0, result.StructuredContent!.Value.GetProperty("totalConnected").GetInt32());
    }

    [TestMethod]
    public void McpTaskExecutionSelector_UsesPerToolMeta()
    {
        var broker = new Mock<IHostBroker>();
        var invoke = InvokeDynamicTool.Create(broker.Object);
        var search = SearchDynamicTool.Create(broker.Object);
        var optional = TaskModeFixture.CreateOptionalTool("execute_csharp_code");

        Assert.AreEqual(
            McpTaskExecutionMode.Synchronous,
            McpTaskExecutionMeta.SelectForRequest(McpServerConfigurationTests.CreateToolRequest(invoke)));
        Assert.AreEqual(
            McpTaskExecutionMode.Synchronous,
            McpTaskExecutionMeta.SelectForRequest(McpServerConfigurationTests.CreateToolRequest(search)));
        Assert.AreEqual(
            McpTaskExecutionMode.Optional,
            McpTaskExecutionMeta.SelectForRequest(McpServerConfigurationTests.CreateToolRequest(optional)));
        Assert.AreEqual(
            McpTaskExecutionMode.Synchronous,
            McpTaskExecutionMeta.SelectForRequest(CreateRequestContext("unknown_tool")));
        Assert.AreEqual(
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
