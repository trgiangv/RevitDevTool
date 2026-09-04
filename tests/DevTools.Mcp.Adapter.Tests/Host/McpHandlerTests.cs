using System.Text.Json.Nodes;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.Catalog;
using DevTools.Mcp.Core;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Adapter.Tests.Harness;
using DevTools.Settings;
using DevTools.Settings.Configs;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Mcp.Adapter.Tests.Host;

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
        Assert.Equal((int)ModelContextProtocol.McpErrorCode.MethodNotFound, error["code"]!.GetValue<int>());
        Assert.Contains(RequestMethods.ServerDiscover, error["message"]!.GetValue<string>(), StringComparison.Ordinal);
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
        Assert.Equal((int)ModelContextProtocol.McpErrorCode.InvalidParams, error["code"]!.GetValue<int>());
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
                    [MetaKeys.ProtocolVersion] = "2025-11-25",
                },
            },
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        var error = response!["error"]!.AsObject();
        Assert.Equal((int)ModelContextProtocol.McpErrorCode.UnsupportedProtocolVersion, error["code"]!.GetValue<int>());
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
            It.IsAny<CallToolRequestParams>(),
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
        Assert.Equal((int)ModelContextProtocol.McpErrorCode.InvalidParams, error["code"]!.GetValue<int>());
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
        Assert.Equal((int)ModelContextProtocol.McpErrorCode.MethodNotFound, error["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task Ping_WithCurrentProtocol_ReturnsEmptyResult()
    {
        var handler = McpHostTestHarness.CreateHandler(McpHostTestHarness.CreateCatalogStore());
        var response = await handler.HandleAsync(
            McpHostTestHarness.CreateRequest(RequestMethods.Ping),
            TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Null(response!["error"]);
        Assert.NotNull(response["result"]);
    }

    [Fact]
    public async Task ResourcesList_ReturnsCatalogResources()
    {
        var catalogStore = McpHostTestHarness.CreateCatalogStore();
        var loader = new Mock<IMcpCatalogLoader>();
        loader
            .Setup(l => l.LoadCatalog(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(new McpRegistryCatalog
            {
                Tools = [],
                Resources =
                [
                    new McpRegisteredResource
                    {
                        Id = "demo",
                        Descriptor = new Resource { Name = "demo", Uri = "sample://demo/status" },
                        Binding = McpPrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", "Read"),
                    },
                ],
            });
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.McpRegistryConfig).Returns(new McpRegistryConfig());
        var store = new McpCatalogStore(loader.Object, settings.Object);
        var handler = McpHostTestHarness.CreateHandler(store);

        var response = await handler.HandleAsync(
            McpHostTestHarness.CreateRequest(RequestMethods.ResourcesList),
            TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        var resources = response!["result"]!["resources"]!.AsArray();
        Assert.Single(resources);
        Assert.Equal("sample://demo/status", resources[0]!["uri"]!.GetValue<string>());
    }

    [Fact]
    public async Task ResourceTemplatesList_ReturnsCatalogTemplates()
    {
        var loader = new Mock<IMcpCatalogLoader>();
        loader
            .Setup(l => l.LoadCatalog(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(new McpRegistryCatalog
            {
                Tools = [],
                Resources =
                [
                    new McpRegisteredResource
                    {
                        Id = "template",
                        TemplateDescriptor = new ResourceTemplate
                        {
                            Name = "demo_template",
                            UriTemplate = "sample://{id}",
                        },
                        Binding = McpPrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", "Read"),
                    },
                ],
            });
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.McpRegistryConfig).Returns(new McpRegistryConfig());
        var store = new McpCatalogStore(loader.Object, settings.Object);
        var handler = McpHostTestHarness.CreateHandler(store);

        var response = await handler.HandleAsync(
            McpHostTestHarness.CreateRequest(RequestMethods.ResourcesTemplatesList),
            TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        var templates = response!["result"]!["resourceTemplates"]!.AsArray();
        Assert.Single(templates);
        Assert.Equal("sample://{id}", templates[0]!["uriTemplate"]!.GetValue<string>());
    }
}
