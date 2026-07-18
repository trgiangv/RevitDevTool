using System.Text.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using DevTools.Mcp;
using DevTools.Mcp.Routing;
using DevTools.Mcp.Routing.Catalog;
using DevTools.Mcp.Routing.Broker;
using DevTools.Daemon.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server.Tests;

public sealed class CatalogServiceSnapshotTests
{
    [Fact]
    public async Task RebuildCatalog_RetainsOnlyFailingHostsPriorSnapshot()
    {
        var healthy = new SnapshotSession(5201, "healthy_tool");
        var failing = new SnapshotSession(5202, "failing_tool");
        var manager = new SnapshotInstanceManager([healthy, failing]);
        var broker = new BrokerCatalogIndex();
        var catalog = CreateCatalog(manager, broker);

        await catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);
        failing.FailLists = true;
        await catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);

        Assert.Contains(broker.Search(new BrokerSearchRequest(null, null, null)).Items, entry => entry.Name == "healthy_tool");
        Assert.Contains(broker.Search(new BrokerSearchRequest(null, null, null)).Items, entry => entry.Name == "failing_tool");
    }

    [Fact]
    public async Task RebuildCatalog_RemovesFailedHostsSnapshotOnlyAfterDisconnect()
    {
        var healthy = new SnapshotSession(5203, "healthy_tool");
        var failing = new SnapshotSession(5204, "failing_tool");
        var manager = new SnapshotInstanceManager([healthy, failing]);
        var broker = new BrokerCatalogIndex();
        var catalog = CreateCatalog(manager, broker);

        await catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);
        failing.FailLists = true;
        await catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);
        manager.Remove(failing);
        await catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);

        Assert.Contains(broker.Search(new BrokerSearchRequest(null, null, null)).Items, entry => entry.Name == "healthy_tool");
        Assert.DoesNotContain(broker.Search(new BrokerSearchRequest(null, null, null)).Items, entry => entry.Name == "failing_tool");
    }

    [Fact]
    public async Task RebuildCatalog_NativeSurfaceUpdatesResourceCollectionAndRaisesListChanges()
    {
        var session = new NativeSnapshotSession(5205);
        var manager = new SnapshotInstanceManager([session]);
        var broker = new BrokerCatalogIndex();
        McpServerResourceCollection resources = [];
        var changes = 0;
        resources.Changed += (_, _) => changes++;
        var catalog = new CatalogService(
            manager,
            [],
            [],
            resources,
            broker,
            nativeSurface: true,
            [],
            NullLogger<CatalogService>.Instance,
            CancellationToken.None);

        await catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, resources.Count);
        Assert.Contains(resources, resource => resource.ProtocolResource?.Uri ==
            "devtools://host/5205/resource/cmV2aXQ6Ly9tb2RlbC9jb250ZXh0");
        Assert.Contains(resources, resource => resource.ProtocolResourceTemplate.UriTemplate.Contains("{id}", StringComparison.Ordinal));
        Assert.True(changes >= 3);

        session.IncludeDirectResource = false;
        var changesBeforeRefresh = changes;
        await catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);

        Assert.Single(resources);
        Assert.True(changes > changesBeforeRefresh);
    }

    private static CatalogService CreateCatalog(SnapshotInstanceManager manager, BrokerCatalogIndex broker)
    {
        McpServerPrimitiveCollection<McpServerTool> tools = [];
        McpServerPrimitiveCollection<McpServerPrompt> prompts = [];
        McpServerResourceCollection resources = [];
        return new CatalogService(
            manager,
            tools,
            prompts,
            resources,
            broker,
            nativeSurface: false,
            [],
            NullLogger<CatalogService>.Instance,
            CancellationToken.None);
    }

    private sealed class SnapshotInstanceManager(IEnumerable<IHostMcpSession> sessions) : IInstanceManager
    {
        private readonly List<IHostMcpSession> sessions = [.. sessions];

        public IReadOnlyCollection<IHostMcpSession> Sessions => sessions;
        public event Action? SessionsChanged { add { } remove { } }
        public IHostMcpSession? GetSessionByProcessId(int processId) =>
            sessions.SingleOrDefault(session => session.Instance.ProcessId == processId);
        public void Remove(IHostMcpSession session) => sessions.Remove(session);
    }

    private sealed class SnapshotSession(int processId, string toolName) : IHostMcpSession
    {
        private readonly McpClientTool tool = CreateTool(toolName);

        public HostInstanceDescriptor Instance { get; } = new(
            processId,
            "Test",
            "1.0",
            McpPipeName.Format(processId));
        public bool IsConnected => true;
        public bool FailLists { get; set; }
        public event Action? CatalogChanged { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct) =>
            FailLists
                ? Task.FromException<IList<McpClientTool>>(new IOException("Simulated list failure."))
                : Task.FromResult<IList<McpClientTool>>([tool]);

        public Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientPrompt>>([]);
        public Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientResource>>([]);
        public Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientResourceTemplate>>([]);
        public Task<CallToolResult> CallToolAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<GetPromptResult> GetPromptAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct) =>
            throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static McpClientTool CreateTool(string name)
        {
            var tool = (McpClientTool)RuntimeHelpers.GetUninitializedObject(typeof(McpClientTool));
            var protocolTool = typeof(McpClientTool).GetField(
                "<ProtocolTool>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            protocolTool.SetValue(tool, new Tool
            {
                Name = name,
                InputSchema = JsonSerializer.SerializeToElement(new { type = "object" })
            });
            return tool;
        }
    }

    private sealed class NativeSnapshotSession(int processId) : IHostMcpSession
    {
        private readonly McpClientResource direct = CreateResource("revit://model/context", "model_context");
        private readonly McpClientResourceTemplate template = CreateTemplate("revit://model/elements/{id}", "element");

        public HostInstanceDescriptor Instance { get; } = new(processId, "Test", "1.0", McpPipeName.Format(processId));
        public bool IsConnected => true;
        public bool IncludeDirectResource { get; set; } = true;
        public event Action? CatalogChanged { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct) => Task.FromResult<IList<McpClientTool>>([]);
        public Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct) => Task.FromResult<IList<McpClientPrompt>>([]);
        public Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientResource>>(IncludeDirectResource ? [direct] : []);
        public Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientResourceTemplate>>([template]);
        public Task<CallToolResult> CallToolAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) => throw new NotSupportedException();
        public Task<GetPromptResult> GetPromptAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) => throw new NotSupportedException();
        public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static McpClientResource CreateResource(string uri, string name)
        {
            var resource = (McpClientResource)RuntimeHelpers.GetUninitializedObject(typeof(McpClientResource));
            typeof(McpClientResource).GetField("<ProtocolResource>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(resource, new Resource { Uri = uri, Name = name });
            return resource;
        }

        private static McpClientResourceTemplate CreateTemplate(string uriTemplate, string name)
        {
            var template = (McpClientResourceTemplate)RuntimeHelpers.GetUninitializedObject(typeof(McpClientResourceTemplate));
            typeof(McpClientResourceTemplate).GetField("<ProtocolResourceTemplate>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(template, new ResourceTemplate { UriTemplate = uriTemplate, Name = name });
            return template;
        }
    }
}
