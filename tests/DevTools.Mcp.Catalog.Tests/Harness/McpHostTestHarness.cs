using System.Text.Json.Nodes;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.Adapter.Host;
using DevTools.Mcp.Catalog;
using DevTools.Mcp.Core;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Utils;
using DevTools.Settings;
using DevTools.Settings.Configs;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Mcp.Catalog.Tests.Harness;

internal static class McpHostTestHarness
{
    public static McpHandler CreateHandler(
        McpCatalogStore catalogStore,
        Mock<IMcpPrimitiveDispatcher>? dispatcher = null,
        Mock<IMcpExecutionTracker>? tracker = null,
        Mock<IHostContextExecutor>? hostContext = null,
        McpHandlerOptions? options = null)
    {
        dispatcher ??= new Mock<IMcpPrimitiveDispatcher>();
        tracker ??= CreateExecutionTracker();
        hostContext ??= new Mock<IHostContextExecutor>();

        return new McpHandler(
            catalogStore,
            dispatcher.Object,
            tracker.Object,
            hostContext.Object,
            NullLogger<McpHandler>.Instance,
            options);
    }

    public static McpCatalogStore CreateCatalogStore(params McpRegisteredTool[] tools)
    {
        var catalog = new McpRegistryCatalog
        {
            Tools = tools,
            Resources = [],
        };

        var loader = new Mock<IMcpCatalogLoader>();
        loader
            .Setup(l => l.LoadCatalog(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(catalog);

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.McpRegistryConfig).Returns(new McpRegistryConfig());

        return new McpCatalogStore(loader.Object, settings.Object);
    }

    public static Mock<IMcpExecutionTracker> CreateExecutionTracker()
    {
        var tracker = new Mock<IMcpExecutionTracker>();
        tracker.Setup(t => t.BeginExecution(It.IsAny<string>())).Returns(Mock.Of<IDisposable>());
        return tracker;
    }

    public static McpRegisteredTool CreateRegisteredTool(string name, string? description = null) => new()
    {
        Id = name,
        Descriptor = new Tool
        {
            Name = name,
            Description = description ?? $"{name} description",
            InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }),
        },
        Binding = McpPrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", name),
    };

    public static JsonObject WithCurrentProtocol(JsonObject? parameters = null)
    {
        parameters ??= new JsonObject();
        parameters[McpSpecKeys.Meta.Key] = new JsonObject
        {
            [MetaKeys.ProtocolVersion] = McpSpecKeys.ProtocolVersions.Current,
        };
        return parameters;
    }

    public static JsonObject CreateRequest(string method, JsonObject? parameters = null, int id = 1) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["method"] = method,
        ["params"] = WithCurrentProtocol(parameters),
    };
}
