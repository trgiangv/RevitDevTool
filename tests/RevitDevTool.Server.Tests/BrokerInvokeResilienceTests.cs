using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DevTools.Mcp;
using DevTools.Daemon.Mcp.Tools;
using DevTools.Mcp.Routing;
using DevTools.Mcp.Routing.Broker;
using DevTools.Mcp.Routing.Catalog;
using ModelContextProtocol.Client;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace RevitDevTool.Server.Tests;

public sealed class BrokerInvokeResilienceTests
{
    [Fact]
    public async Task AmbiguousTarget_IsReadableNonErrorWithCandidates()
    {
        var first = new TestSession(5103, "execute_csharp_code");
        var second = new TestSession(5104, "execute_csharp_code");
        var catalog = CreateCatalog(first, second);

        var result = await catalog.InvokeAsync(
            new TestManager(first, second),
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"),
            null,
            null,
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        var payload = DeserializePayload(result);
        Assert.Equal(BrokerInvokeStatus.HostSelectionRequired, payload.Status);
        Assert.Equal([5103, 5104], payload.Candidates.Select(candidate => candidate.HostId));
        Assert.All(payload.Candidates, candidate =>
        {
            Assert.Equal("TestHost", candidate.HostApp);
            Assert.Equal("1.0", candidate.VersionNumber);
        });
        Assert.Equal(0, first.CallCount);
        Assert.Equal(0, second.CallCount);
    }

    [Fact]
    public async Task WrongHostId_ReturnsActualHostsWithoutSecondSearch()
    {
        var actual = new TestSession(41100, "execute_csharp_code");
        var catalog = CreateCatalog(actual);

        var result = await catalog.InvokeAsync(
            new TestManager(actual),
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"),
            23856,
            null,
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        var payload = DeserializePayload(result);
        Assert.Equal(BrokerInvokeStatus.HostMismatch, payload.Status);
        Assert.Equal(41100, Assert.Single(payload.Candidates).HostId);
        Assert.Equal(0, actual.CallCount);
    }

    [Fact]
    public async Task MissingTarget_IsStructuredErrorWithoutExecutionUncertainty()
    {
        var session = new TestSession(41108, "another_tool");
        var catalog = CreateCatalog(session);

        var result = await catalog.InvokeAsync(
            new TestManager(session),
            BrokerPrimitiveTarget.Parse("tool:missing_tool"),
            null,
            null,
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        var payload = DeserializePayload(result);
        Assert.Equal(BrokerInvokeStatus.TargetNotFound, payload.Status);
        Assert.False(payload.MayHaveExecuted);
        Assert.Empty(payload.Candidates);
        Assert.Equal(0, session.CallCount);
    }

    [Fact]
    public void StatusConstants_AreFrozenProtocolValues()
    {
        Assert.Equal("host_selection_required", BrokerInvokeStatus.HostSelectionRequired);
        Assert.Equal("host_mismatch", BrokerInvokeStatus.HostMismatch);
        Assert.Equal("target_not_found", BrokerInvokeStatus.TargetNotFound);
        Assert.Equal("host_disconnected", BrokerInvokeStatus.HostDisconnected);
        Assert.Equal("connection_lost", BrokerInvokeStatus.ConnectionLost);
        Assert.Equal("timed_out", BrokerInvokeStatus.TimedOut);
        Assert.Equal("host_failed", BrokerInvokeStatus.HostFailed);
    }

    [Fact]
    public async Task StaleGeneration_ReturnsPreDispatchDisconnectWithoutCallingReplacement()
    {
        var published = new TestSession(41101, "execute_csharp_code", generation: 1);
        var replacement = new TestSession(41101, "execute_csharp_code", generation: 2);
        var catalog = CreateCatalog(published);
        var manager = new TestManager(replacement);

        var result = await catalog.InvokeAsync(
            manager,
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"),
            41101,
            null,
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        var payload = DeserializePayload(result);
        Assert.Equal(BrokerInvokeStatus.HostDisconnected, payload.Status);
        Assert.False(payload.MayHaveExecuted);
        Assert.Equal([(41101, 1)], manager.ExactLookups);
        Assert.Equal(0, replacement.CallCount);
    }

    [Fact]
    public async Task DisconnectedExactGeneration_IsPreDispatchFailure()
    {
        var session = new TestSession(41102, "execute_csharp_code", isConnected: false);
        var catalog = CreateCatalog(session);

        var result = await catalog.InvokeAsync(
            new TestManager(session),
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"),
            null,
            null,
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        var payload = DeserializePayload(result);
        Assert.Equal(BrokerInvokeStatus.HostDisconnected, payload.Status);
        Assert.False(payload.MayHaveExecuted);
        Assert.Equal(0, session.CallCount);
    }

    [Fact]
    public async Task DeadlineCancellation_ReturnsTimedOutWithExecutionUncertainty()
    {
        var session = new TestSession(41103, "execute_csharp_code", invocation: WaitForBrokerCancellationAsync);
        var catalog = CreateCatalog(session);

        var result = await catalog.InvokeAsync(
            new TestManager(session),
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"),
            null,
            null,
            TimeSpan.FromMilliseconds(25),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        var payload = DeserializePayload(result);
        Assert.Equal(BrokerInvokeStatus.TimedOut, payload.Status);
        Assert.True(payload.MayHaveExecuted);
        Assert.Equal(1, session.CallCount);
    }

    [Fact]
    public async Task ClientCancellation_PropagatesInsteadOfBecomingTimeout()
    {
        using var cancellation = new CancellationTokenSource();
        var session = new TestSession(41104, "execute_csharp_code", invocation: async ct =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new CallToolResult();
        });
        var catalog = CreateCatalog(session);

        var invocation = catalog.InvokeAsync(
            new TestManager(session),
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"),
            null,
            null,
            TimeSpan.FromMinutes(5),
            cancellation.Token);
        await session.InvocationEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation);
        Assert.Equal(1, session.CallCount);
    }

    [Fact]
    public async Task PreCancelledClientRequest_DoesNotCrossDispatchBoundary()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var session = new TestSession(41105, "execute_csharp_code");
        var catalog = CreateCatalog(session);

        var invocation = catalog.InvokeAsync(
            new TestManager(session),
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"),
            null,
            null,
            TimeSpan.FromMinutes(5),
            cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation);
        Assert.Equal(0, session.CallCount);
    }

    [Fact]
    public async Task ArgumentConversionFailure_IsPreDispatchFailure()
    {
        using var document = JsonDocument.Parse("{\"duplicate\":1,\"duplicate\":2}");
        var session = new TestSession(41109, "execute_csharp_code");
        var catalog = CreateCatalog(session);

        var result = await catalog.InvokeAsync(
            new TestManager(session),
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"),
            null,
            document.RootElement,
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        var payload = DeserializePayload(result);
        Assert.Equal(BrokerInvokeStatus.HostFailed, payload.Status);
        Assert.False(payload.MayHaveExecuted);
        Assert.Equal(0, session.CallCount);
    }

    [Fact]
    public async Task GenerationReplacementDuringConversion_IsRejectedBeforeDispatch()
    {
        var published = new TestSession(41110, "execute_csharp_code", generation: 1);
        var replacement = new TestSession(41110, "execute_csharp_code", generation: 2);
        var manager = new MutableManager(published);
        var catalog = CreateCatalogWithConverter(arguments =>
        {
            manager.Replace(replacement);
            return BrokerArgumentConverter.ToObjects(arguments);
        }, published);

        var result = await catalog.InvokeAsync(
            manager,
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"),
            null,
            JsonSerializer.SerializeToElement(new { value = 42 }),
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        var payload = DeserializePayload(result);
        Assert.Equal(BrokerInvokeStatus.HostDisconnected, payload.Status);
        Assert.False(payload.MayHaveExecuted);
        Assert.Equal(0, published.CallCount);
        Assert.Equal(0, replacement.CallCount);
    }

    [Fact]
    public async Task ClientCancellationDuringConversion_PropagatesBeforeDispatch()
    {
        using var cancellation = new CancellationTokenSource();
        var session = new TestSession(41111, "execute_csharp_code");
        var catalog = CreateCatalogWithConverter(arguments =>
        {
            cancellation.Cancel();
            return BrokerArgumentConverter.ToObjects(arguments);
        }, session);

        var invocation = catalog.InvokeAsync(
            new TestManager(session),
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"),
            null,
            JsonSerializer.SerializeToElement(new { value = 42 }),
            TimeSpan.FromMinutes(5),
            cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation);
        Assert.Equal(0, session.CallCount);
    }

    [Theory]
    [InlineData("io", BrokerInvokeStatus.ConnectionLost)]
    [InlineData("disposed", BrokerInvokeStatus.ConnectionLost)]
    [InlineData("host", BrokerInvokeStatus.HostFailed)]
    public async Task PostDispatchFailure_IsClassifiedWithoutRetry(string failureKind, string expectedStatus)
    {
        var session = new TestSession(41106, "execute_csharp_code", invocation: _ => failureKind switch
        {
            "io" => Task.FromException<CallToolResult>(new IOException("pipe closed")),
            "disposed" => Task.FromException<CallToolResult>(new ObjectDisposedException("pipe")),
            _ => Task.FromException<CallToolResult>(new InvalidOperationException("host failed"))
        });
        var catalog = CreateCatalog(session);

        var result = await catalog.InvokeAsync(
            new TestManager(session),
            BrokerPrimitiveTarget.Parse("tool:execute_csharp_code"),
            null,
            null,
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        var payload = DeserializePayload(result);
        Assert.Equal(expectedStatus, payload.Status);
        Assert.True(payload.MayHaveExecuted);
        Assert.Equal(1, session.CallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(901)]
    public async Task PublicTool_RejectsOutOfRangeTimeout(int timeoutSeconds)
    {
        var session = new TestSession(41107, "execute_csharp_code");
        var tools = new DevToolsBrokerTools(CreateCatalog(session), new TestManager(session));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            tools.InvokeAsync(
                "tool:execute_csharp_code",
                timeoutSeconds: timeoutSeconds,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    private static BrokerInvokePayload DeserializePayload(CallToolResult result)
    {
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        return Assert.IsType<BrokerInvokePayload>(
            JsonSerializer.Deserialize<BrokerInvokePayload>(structured, McpJsonUtilities.DefaultOptions));
    }

    private static async Task<CallToolResult> WaitForBrokerCancellationAsync(CancellationToken ct)
    {
        var cancellation = Task.Delay(Timeout.InfiniteTimeSpan, ct);
        var fallback = Task.Delay(TimeSpan.FromMilliseconds(500));
        if (await Task.WhenAny(cancellation, fallback) == fallback)
            throw new InvalidOperationException("The broker did not enforce its deadline.");
        await cancellation;
        throw new InvalidOperationException("Cancellation was not observed.");
    }

    private static BrokerCatalogIndex CreateCatalog(params TestSession[] sessions)
    {
        var catalog = new BrokerCatalogIndex();
        Publish(catalog, sessions);
        return catalog;
    }

    private static BrokerCatalogIndex CreateCatalogWithConverter(
        Func<JsonElement?, IReadOnlyDictionary<string, object?>?> argumentConverter,
        params TestSession[] sessions)
    {
        var constructor = typeof(BrokerCatalogIndex)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 2);
        var catalog = Assert.IsType<BrokerCatalogIndex>(constructor.Invoke([null, argumentConverter]));
        Publish(catalog, sessions);
        return catalog;
    }

    private static void Publish(BrokerCatalogIndex catalog, params TestSession[] sessions)
    {
        catalog.ReplacePublications(sessions.Select(session => new HostCatalogPublication(
            new HostCatalogIdentity(session.Instance.PipeName, session.Generation),
            session.Instance,
            HostCatalogState.Ready,
            session.Snapshot,
            DateTimeOffset.UtcNow,
            null,
            null)));
    }

    private sealed class MutableManager(IHostMcpSession current) : IInstanceManager
    {
        private IHostMcpSession current = current;
        public IReadOnlyCollection<IHostMcpSession> Sessions => [current];
        public event Action? SessionsChanged { add { } remove { } }
        public void Replace(IHostMcpSession replacement) => current = replacement;
        public IHostMcpSession? GetSessionByProcessId(int processId) =>
            current.Instance.ProcessId == processId ? current : null;
        public IHostMcpSession? GetSession(int processId, int generation) =>
            current.Instance.ProcessId == processId && current.Generation == generation ? current : null;
    }

    private sealed class TestManager(params IHostMcpSession[] sessions) : IInstanceManager
    {
        public IReadOnlyCollection<IHostMcpSession> Sessions { get; } = sessions;
        public List<(int ProcessId, int Generation)> ExactLookups { get; } = [];
        public event Action? SessionsChanged { add { } remove { } }

        public IHostMcpSession? GetSessionByProcessId(int processId) =>
            Sessions.SingleOrDefault(session => session.Instance.ProcessId == processId);

        public IHostMcpSession? GetSession(int processId, int generation)
        {
            ExactLookups.Add((processId, generation));
            return Sessions.SingleOrDefault(session => session.Instance.ProcessId == processId && session.Generation == generation);
        }
    }

    private sealed class TestSession(
        int processId,
        string toolName,
        int generation = 1,
        bool isConnected = true,
        Func<CancellationToken, Task<CallToolResult>>? invocation = null) : IHostMcpSession
    {
        private readonly McpClientTool tool = CreateTool(toolName);

        public HostInstanceDescriptor Instance { get; } =
            new(processId, "TestHost", "1.0", McpPipeName.Format(processId));
        public int Generation { get; } = generation;
        public bool IsConnected => isConnected;
        public int CallCount { get; private set; }
        public TaskCompletionSource<bool> InvocationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public HostCatalogSnapshot Snapshot => HostCatalogSnapshot.Create(Instance, [tool], [], [], []);
        public event Action? CatalogChanged { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientTool>>([tool]);
        public Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientPrompt>>([]);
        public Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientResource>>([]);
        public Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientResourceTemplate>>([]);
        public Task<CallToolResult> CallToolAsync(
            string name,
            IReadOnlyDictionary<string, object?>? arguments,
            CancellationToken ct)
        {
            CallCount++;
            InvocationEntered.TrySetResult(true);
            if (invocation is not null)
                return invocation(ct);
            return Task.FromResult(new CallToolResult { Content = [new TextContentBlock { Text = name }] });
        }
        public Task<GetPromptResult> GetPromptAsync(
            string name,
            IReadOnlyDictionary<string, object?>? arguments,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct) =>
            throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static McpClientTool CreateTool(string name)
        {
            var result = (McpClientTool)RuntimeHelpers.GetUninitializedObject(typeof(McpClientTool));
            typeof(McpClientTool).GetField("<ProtocolTool>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(result, new Tool
                {
                    Name = name,
                    Description = $"Description for {name}",
                    InputSchema = JsonSerializer.SerializeToElement(new { type = "object" })
                });
            return result;
        }
    }
}
