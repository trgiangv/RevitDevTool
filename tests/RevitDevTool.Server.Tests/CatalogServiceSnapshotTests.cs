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
    public async Task RebuildCatalog_FetchesHostsConcurrently()
    {
        var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new SnapshotSession(5101, "first_tool", fetchBarrier: barrier);
        var second = new SnapshotSession(5102, "second_tool", fetchBarrier: barrier);
        var catalog = CreateCatalog(new SnapshotInstanceManager([first, second]), new BrokerCatalogIndex());

        var rebuild = catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);
        try
        {
            await Task.WhenAll(first.FetchEntered.Task, second.FetchEntered.Task)
                .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        }
        finally
        {
            barrier.TrySetResult(true);
            await rebuild;
        }
    }

    [Fact]
    public async Task ReconnectFailure_DoesNotPublishPriorGenerationSnapshot()
    {
        var manager = new SnapshotInstanceManager([new SnapshotSession(5103, "old_tool", generation: 1)]);
        var broker = new BrokerCatalogIndex();
        var catalog = CreateCatalog(manager, broker);
        await catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);

        manager.SetSessions([new SnapshotSession(5103, "new_tool", generation: 2) { FailLists = true }]);
        await catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);

        var search = broker.Search(new BrokerSearchRequest(null, null, null));
        Assert.DoesNotContain(search.Items, item => item.Name == "old_tool");
        var status = Assert.Single(search.Catalogs!);
        Assert.Equal(HostCatalogState.Unavailable, status.State);
        Assert.Equal("catalog_fetch_failed", status.LastErrorCode);
        Assert.DoesNotContain("Simulated", status.LastErrorCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RebuildCatalog_PublishesNewIdentityRefreshingThenReady()
    {
        var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new SnapshotSession(5104, "ready_tool", generation: 3, fetchBarrier: barrier);
        var broker = new BrokerCatalogIndex();
        var catalog = CreateCatalog(new SnapshotInstanceManager([session]), broker);
        var changes = new List<HostCatalogPublication>();
        catalog.PublicationChanged += changes.Add;

        var rebuild = catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);
        await session.FetchEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        var refreshing = Assert.Single(changes);
        Assert.Equal(new HostCatalogIdentity(session.Instance.PipeName, 3), refreshing.Identity);
        Assert.Equal(HostCatalogState.Refreshing, refreshing.State);
        Assert.Equal(HostCatalogState.Refreshing, Assert.Single(
            broker.Search(new BrokerSearchRequest(null, null, null)).Catalogs!).State);

        barrier.SetResult(true);
        await rebuild;

        Assert.Collection(changes,
            publication => Assert.Equal(HostCatalogState.Refreshing, publication.State),
            publication => Assert.Equal(HostCatalogState.Ready, publication.State));
        Assert.Equal(HostCatalogState.Ready, Assert.Single(
            broker.Search(new BrokerSearchRequest(null, null, null)).Catalogs!).State);
    }

    [Fact]
    public async Task RebuildCatalog_RetainsOnlyFailingHostsPriorSnapshot()
    {
        var healthy = new SnapshotSession(5201, "healthy_tool");
        var failing = new SnapshotSession(5202, "failing_tool");
        var manager = new SnapshotInstanceManager([healthy, failing]);
        var broker = new BrokerCatalogIndex();
        var catalog = CreateCatalog(manager, broker);

        await catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);
        var expectedRevision = broker.Search(new BrokerSearchRequest(null, null, null)).Revision;
        failing.FailLists = true;
        await catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);

        Assert.Contains(broker.Search(new BrokerSearchRequest(null, null, null)).Items, entry => entry.Name == "healthy_tool");
        Assert.Contains(broker.Search(new BrokerSearchRequest(null, null, null)).Items, entry => entry.Name == "failing_tool");
        Assert.Equal(expectedRevision, broker.Search(new BrokerSearchRequest(null, null, null)).Revision);
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
    public async Task RebuildCatalog_CancelledDuringFetch_DoesNotApplyNewSnapshot()
    {
        var session = new SnapshotSession(5206, "before_cancellation");
        var manager = new SnapshotInstanceManager([session]);
        var broker = new BrokerCatalogIndex();
        var catalog = CreateCatalog(manager, broker);
        await catalog.RebuildCatalogAsync(TestContext.Current.CancellationToken);
        var before = broker.Search(new BrokerSearchRequest(null, null, null));

        using var cancellation = new CancellationTokenSource();
        session.ToolName = "after_cancellation";
        session.CancelOnNextFetch = cancellation;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.RebuildCatalogAsync(cancellation.Token));

        var after = broker.Search(new BrokerSearchRequest(null, null, null));
        Assert.Equal(before.Revision, after.Revision);
        Assert.Contains(after.Items, entry => entry.Name == "before_cancellation");
        Assert.DoesNotContain(after.Items, entry => entry.Name == "after_cancellation");
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

        public IHostMcpSession? GetSession(int processId, int generation) =>
            GetSessionByProcessId(processId) is { Generation: var actual } session && actual == generation
                ? session
                : null;
        public void Remove(IHostMcpSession session) => sessions.Remove(session);
        public void SetSessions(IEnumerable<IHostMcpSession> replacements)
        {
            sessions.Clear();
            sessions.AddRange(replacements);
        }
    }

    private sealed class SnapshotSession(
        int processId,
        string toolName,
        int generation = 1,
        TaskCompletionSource<bool>? fetchBarrier = null) : IHostMcpSession
    {
        private McpClientTool tool = CreateTool(toolName);

        public HostInstanceDescriptor Instance { get; } = new(
            processId,
            "Test",
            "1.0",
            HostPipeName.Format("Test", "1.0", processId));
        public int Generation { get; } = generation;
        public bool IsConnected => true;
        public bool FailLists { get; set; }
        public string ToolName { set => tool = CreateTool(value); }
        public CancellationTokenSource? CancelOnNextFetch { get; set; }
        public TaskCompletionSource<bool> FetchEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event Action? CatalogChanged { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public async Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct)
        {
            FetchEntered.TrySetResult(true);
            if (fetchBarrier is not null)
                await fetchBarrier.Task.WaitAsync(ct);
            if (FailLists)
                throw new IOException("Simulated list failure.");
            return [tool];
        }

        public Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientPrompt>>([]);
        public Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientResource>>([]);
        public Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct)
        {
            CancelOnNextFetch?.Cancel();
            CancelOnNextFetch = null;
            return Task.FromResult<IList<McpClientResourceTemplate>>([]);
        }
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

        public HostInstanceDescriptor Instance { get; } = new(processId, "Test", "1.0", HostPipeName.Format("Test", "1.0", processId));
        public int Generation { get; init; } = 1;
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
