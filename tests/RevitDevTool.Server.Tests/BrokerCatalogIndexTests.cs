using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DevTools.Mcp;
using DevTools.Mcp.Routing;
using DevTools.Mcp.Routing.Broker;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace RevitDevTool.Server.Tests;

public sealed class BrokerCatalogIndexTests_PrimitiveTarget
{
    [Theory]
    [InlineData("tool:execute_csharp_code", BrokerPrimitiveKind.Tool, "execute_csharp_code")]
    [InlineData("resource:revit://model/context", BrokerPrimitiveKind.Resource, "revit://model/context")]
    [InlineData("prompt:revit_code", BrokerPrimitiveKind.Prompt, "revit_code")]
    public void ParseTarget_RoundTrips(string value, BrokerPrimitiveKind kind, string key)
    {
        var target = BrokerPrimitiveTarget.Parse(value);

        Assert.Equal(kind, target.Kind);
        Assert.Equal(key, target.Key);
        Assert.Equal(value, target.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("tool:")]
    [InlineData("unknown:name")]
    public void ParseTarget_RejectsInvalidValues(string value) =>
        Assert.Throws<ArgumentException>(() => BrokerPrimitiveTarget.Parse(value));
}

public sealed class BrokerCatalogIndexTests
{
    [Fact]
    public void Search_UsesCachedSnapshotWithoutCallingHost()
    {
        var session = new RecordingSession(5101, "execute_csharp_code");
        var catalog = CreateCatalog(session);

        var result = catalog.Search(new BrokerSearchRequest("execute", null, null));

        Assert.Single(result.Items);
        Assert.Equal(0, session.CallCount);
        Assert.NotNull(result.Items[0].Schema);
    }

    [Fact]
    public async Task Invoke_UniqueTool_CallsItsSessionOnce()
    {
        var session = new RecordingSession(5102, "execute_csharp_code");
        var catalog = CreateCatalog(session);

        var result = await catalog.InvokeAsync(new RecordingManager([session]),
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"), null, null, TestContext.Current.CancellationToken);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(1, session.CallCount);
    }

    [Fact]
    public async Task Invoke_AmbiguousTool_ReturnsCandidatesWithoutCallingHosts()
    {
        var first = new RecordingSession(5103, "execute_csharp_code");
        var second = new RecordingSession(5104, "execute_csharp_code");
        var catalog = CreateCatalog(first, second);

        var result = await catalog.InvokeAsync(new RecordingManager([first, second]),
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"), null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(0, first.CallCount);
        Assert.Equal(0, second.CallCount);
        Assert.Contains("5103", JsonSerializer.Serialize(result.StructuredContent));
        Assert.Contains("5104", JsonSerializer.Serialize(result.StructuredContent));
    }

    [Fact]
    public async Task Invoke_HostIdResolvesAmbiguousTool()
    {
        var first = new RecordingSession(5105, "execute_csharp_code");
        var second = new RecordingSession(5106, "execute_csharp_code");
        var catalog = CreateCatalog(first, second);

        var result = await catalog.InvokeAsync(new RecordingManager([first, second]),
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"), second.Instance.ProcessId, null,
            TestContext.Current.CancellationToken);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(0, first.CallCount);
        Assert.Equal(1, second.CallCount);
    }

    [Fact]
    public void Search_DefaultsToEightItemsAndIncludesSchemas()
    {
        var sessions = Enumerable.Range(0, 10)
            .Select(index => new RecordingSession(5200 + index, $"tool_{index:D2}"))
            .ToArray();
        var catalog = CreateCatalog(sessions);

        var result = catalog.Search(new BrokerSearchRequest(null, null, null));

        Assert.Equal(8, result.Items.Count);
        Assert.True(result.Truncated);
        Assert.All(result.Items, item => Assert.NotNull(item.Schema));
    }

    [Fact]
    public void Search_SummaryOmitsSchemas()
    {
        var catalog = CreateCatalog(new RecordingSession(5301, "execute_csharp_code"));

        var result = catalog.Search(new BrokerSearchRequest(
            "execute", null, null, BrokerSearchDetail.Summary));

        Assert.Single(result.Items);
        Assert.Null(result.Items[0].Schema);
    }

    private static BrokerCatalogIndex CreateCatalog(params RecordingSession[] sessions)
    {
        var catalog = new BrokerCatalogIndex();
        catalog.ReplaceSnapshots(sessions.Select(session => session.Snapshot));
        return catalog;
    }

    private sealed class RecordingManager(IEnumerable<IHostMcpSession> sessions) : IInstanceManager
    {
        private readonly IHostMcpSession[] sessions = [.. sessions];

        public IReadOnlyCollection<IHostMcpSession> Sessions => sessions;
        public event Action? SessionsChanged { add { } remove { } }
        public event Action? Changed { add { } remove { } }
        public IHostMcpSession? GetSessionByProcessId(int processId) =>
            sessions.SingleOrDefault(session => session.Instance.ProcessId == processId);
        public IReadOnlyCollection<InstanceInfo> GetInstances() => [];
        public IHostBridgeClient? GetDefault(string? hostApp = null) => null;
        public IHostBridgeClient? GetByProcessId(int processId) => null;
        public IReadOnlyCollection<string> GetDiscoveredPipeNames() => [];
    }

    private sealed class RecordingSession(int processId, string toolName) : IHostMcpSession
    {
        private readonly McpClientTool tool = CreateTool(toolName);

        public HostInstanceDescriptor Instance { get; } = new(processId, "TestHost", "1.0", McpPipeName.Format(processId));
        public HostCatalogSnapshot Snapshot => HostCatalogSnapshot.Create(Instance, [tool], [], [], []);
        public bool IsConnected => true;
        public int CallCount { get; private set; }
        public event Action? CatalogChanged { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct) => Task.FromResult<IList<McpClientTool>>([tool]);
        public Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct) => Task.FromResult<IList<McpClientPrompt>>([]);
        public Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct) => Task.FromResult<IList<McpClientResource>>([]);
        public Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct) => Task.FromResult<IList<McpClientResourceTemplate>>([]);
        public Task<CallToolResult> CallToolAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new CallToolResult { Content = [new TextContentBlock { Text = name }] });
        }
        public Task<GetPromptResult> GetPromptAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) => throw new NotSupportedException();
        public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static McpClientTool CreateTool(string name)
        {
            var tool = (McpClientTool)RuntimeHelpers.GetUninitializedObject(typeof(McpClientTool));
            typeof(McpClientTool).GetField("<ProtocolTool>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(tool, new Tool
                {
                    Name = name,
                    Description = $"Description for {name}",
                    InputSchema = JsonSerializer.SerializeToElement(new { type = "object" })
                });
            return tool;
        }
    }
}
