using System.Text.Json.Nodes;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Tests.Harness;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Mcp.Tests.Host;

public sealed class McpHandlerTests
{
    [Fact]
    public async Task ServerDiscover_ReturnsServerInfoCapabilitiesAndCurrentVersion()
    {
        var handler = McpHostTestHarness.CreateHandler(McpHostTestHarness.CreateCatalogStore());
        var response = await handler.HandleAsync(
            McpHostTestHarness.CreateDiscoverRequest(),
            TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Null(response!["error"]);
        var result = response!["result"]!.AsObject();
        var supported = result["supportedVersions"]!.AsArray();
        Assert.Single(supported);
        Assert.Equal(McpSpecKeys.ProtocolVersions.Current, supported[0]!.GetValue<string>());
        Assert.Equal("DevTools.Host", result["serverInfo"]!["name"]!.GetValue<string>());
        Assert.True(result["capabilities"]!["tools"]!["listChanged"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Initialize_ReturnsMethodNotFound()
    {
        var handler = McpHostTestHarness.CreateHandler(McpHostTestHarness.CreateCatalogStore());
        var response = await handler.HandleAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = RequestMethods.Initialize,
            ["params"] = new JsonObject
            {
                ["protocolVersion"] = McpSpecKeys.ProtocolVersions.Current,
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject { ["name"] = "test", ["version"] = "1.0.0" },
            },
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        var error = response!["error"]!.AsObject();
        Assert.Equal(McpSpecKeys.JsonRpc.MethodNotFound, error["code"]!.GetValue<int>());
        Assert.Contains(McpSpecKeys.Methods.ServerDiscover, error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolsList_WithoutProtocolMetadata_ReturnsInvalidParams()
    {
        var handler = McpHostTestHarness.CreateHandler(McpHostTestHarness.CreateCatalogStore());
        var response = await handler.HandleAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = RequestMethods.ToolsList,
            ["params"] = new JsonObject(),
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        var error = response!["error"]!.AsObject();
        Assert.Equal(McpSpecKeys.JsonRpc.InvalidParams, error["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task ToolsList_WithUnsupportedVersion_ReturnsUnsupportedProtocolVersion()
    {
        var handler = McpHostTestHarness.CreateHandler(McpHostTestHarness.CreateCatalogStore());
        var response = await handler.HandleAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = RequestMethods.ToolsList,
            ["params"] = new JsonObject
            {
                [McpSpecKeys.Meta.Key] = new JsonObject
                {
                    [McpSpecKeys.Meta.ProtocolVersion] = "2025-11-25",
                },
            },
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        var error = response!["error"]!.AsObject();
        Assert.Equal(McpSpecKeys.JsonRpc.UnsupportedProtocolVersion, error["code"]!.GetValue<int>());
        Assert.Equal("2025-11-25", error["data"]!["requested"]!.GetValue<string>());
    }

    [Fact]
    public async Task ToolsList_ReturnsCatalogTools()
    {
        var catalogStore = McpHostTestHarness.CreateCatalogStore(McpHostTestHarness.CreateRegisteredTool("ping", "Ping tool"));
        var handler = McpHostTestHarness.CreateHandler(catalogStore);

        var response = await handler.HandleAsync(
            McpHostTestHarness.CreateRequest(RequestMethods.ToolsList),
            TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        var tools = response!["result"]!["tools"]!.AsArray();
        Assert.Single(tools);
        Assert.Equal("ping", tools[0]!["name"]!.GetValue<string>());
        Assert.Equal("Ping tool", tools[0]!["description"]!.GetValue<string>());
    }

    [Fact]
    public async Task ToolsCall_DispatchesAndReturnsTextContent()
    {
        var (handler, dispatcher) = McpHostTestHarness.CreateWithTool("ping", "pong");

        var response = await handler.HandleAsync(
            McpHostTestHarness.CreateRequest(RequestMethods.ToolsCall, new JsonObject
            {
                ["name"] = "ping",
                ["arguments"] = new JsonObject(),
            }),
            TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Null(response!["error"]);
        var content = response!["result"]!["content"]!.AsArray();
        Assert.Equal("text", content[0]!["type"]!.GetValue<string>());
        Assert.Equal("pong", content[0]!["text"]!.GetValue<string>());

        dispatcher.Verify(d => d.DispatchToolAsync(
            It.Is<McpRegisteredTool>(tool => tool.Descriptor.Name == "ping"),
            It.IsAny<McpInvocationRequest>(),
            It.IsAny<IHostContextExecutor>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToolsCall_UnknownTool_ReturnsJsonRpcError()
    {
        var handler = McpHostTestHarness.CreateHandler(McpHostTestHarness.CreateCatalogStore(McpHostTestHarness.CreateRegisteredTool("ping")));

        var response = await handler.HandleAsync(
            McpHostTestHarness.CreateRequest(RequestMethods.ToolsCall, new JsonObject { ["name"] = "missing_tool" }),
            TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        var error = response!["error"]!.AsObject();
        Assert.Equal(McpSpecKeys.JsonRpc.InvalidParams, error["code"]!.GetValue<int>());
        Assert.Contains("missing_tool", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownMethod_ReturnsMethodNotFound()
    {
        var handler = McpHostTestHarness.CreateHandler(McpHostTestHarness.CreateCatalogStore());

        var response = await handler.HandleAsync(
            McpHostTestHarness.CreateRequest("does/not/exist"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        var error = response!["error"]!.AsObject();
        Assert.Equal(McpSpecKeys.JsonRpc.MethodNotFound, error["code"]!.GetValue<int>());
    }
}
