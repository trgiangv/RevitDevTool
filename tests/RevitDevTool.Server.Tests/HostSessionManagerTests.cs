using DevTools.Daemon.Mcp;
using DevTools.Mcp.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace RevitDevTool.Server.Tests;

public sealed class HostSessionManagerTests
{
    [Fact]
    public async Task Discovered_ConnectsToConnectedSession()
    {
        var pipeName = McpPipeName.Format(5101);
        var connector = new FakeConnector(pipeName, [ConnectResult.Success()]);
        var clock = new FakeRetryClock();
        await using var manager = CreateManager(pipeName, connector, clock);

        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, connector.AttemptsFor(pipeName));
        Assert.Single(manager.Sessions);
        Assert.Equal(HostSessionState.Connected, Assert.Single(manager.SessionSlots).State);
    }

    [Fact]
    public async Task Backoff_RetriesContinuouslyDiscoveredPipeAndConnects()
    {
        var pipeName = McpPipeName.Format(5102);
        var connector = new FakeConnector(pipeName, [ConnectResult.Failure(), ConnectResult.Success()]);
        var clock = new FakeRetryClock();
        await using var manager = CreateManager(pipeName, connector, clock);

        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);
        Assert.Empty(manager.Sessions);
        Assert.Equal(HostSessionState.Backoff, Assert.Single(manager.SessionSlots).State);

        clock.Advance(TimeSpan.FromSeconds(1));
        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, connector.AttemptsFor(pipeName));
        Assert.Single(manager.Sessions);
        Assert.Equal(HostSessionState.Connected, Assert.Single(manager.SessionSlots).State);
    }

    [Fact]
    public async Task DisconnectedSession_TransitionsThroughBackoffAndReconnects()
    {
        var pipeName = McpPipeName.Format(5103);
        var firstSession = new TestMcpSession(pipeName);
        var connector = new FakeConnector(pipeName,
        [
            ConnectResult.Success(firstSession),
            ConnectResult.Success()
        ]);
        var clock = new FakeRetryClock();
        await using var manager = CreateManager(pipeName, connector, clock);

        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);
        firstSession.Disconnect();
        await Task.Yield();

        Assert.Empty(manager.Sessions);
        Assert.Equal(HostSessionState.Backoff, Assert.Single(manager.SessionSlots).State);

        clock.Advance(TimeSpan.FromSeconds(1));
        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, connector.AttemptsFor(pipeName));
        Assert.Single(manager.Sessions);
        Assert.Equal(HostSessionState.Connected, Assert.Single(manager.SessionSlots).State);
    }

    [Fact]
    public async Task BackoffSlot_IsRemovedWhenPipeDisappears()
    {
        var pipeName = McpPipeName.Format(5104);
        var discoveredPipes = new HashSet<string>([pipeName], StringComparer.OrdinalIgnoreCase);
        var connector = new FakeConnector(pipeName, [ConnectResult.Failure()]);
        var clock = new FakeRetryClock();
        await using var manager = CreateManager(discoveredPipes, connector, clock);

        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HostSessionState.Backoff, Assert.Single(manager.SessionSlots).State);

        discoveredPipes.Clear();
        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(manager.Sessions);
        Assert.Empty(manager.SessionSlots);
    }

    private static HostSessionManager CreateManager(
        string pipeName,
        IHostSessionConnector connector,
        IRetryClock clock) =>
        CreateManager(new HashSet<string>([pipeName], StringComparer.OrdinalIgnoreCase), connector, clock);

    private static HostSessionManager CreateManager(
        HashSet<string> discoveredPipes,
        IHostSessionConnector connector,
        IRetryClock clock) =>
        new(
            NullLogger<HostSessionManager>.Instance,
            NullLoggerFactory.Instance,
            _ => discoveredPipes,
            connector,
            clock);

    private sealed class FakeConnector(string expectedPipeName, IEnumerable<ConnectResult> results) : IHostSessionConnector
    {
        private readonly Queue<ConnectResult> _results = new(results);
        private readonly Dictionary<string, int> _attempts = new(StringComparer.OrdinalIgnoreCase);

        public Task<IHostMcpSession> ConnectAsync(string pipeName, CancellationToken ct)
        {
            Assert.Equal(expectedPipeName, pipeName);
            _attempts[pipeName] = AttemptsFor(pipeName) + 1;
            var result = _results.Dequeue();
            return result.Exception is { } exception
                ? Task.FromException<IHostMcpSession>(exception)
                : Task.FromResult<IHostMcpSession>(result.Session!);
        }

        public int AttemptsFor(string pipeName) => _attempts.GetValueOrDefault(pipeName);
    }

    private sealed record ConnectResult(IHostMcpSession? Session, Exception? Exception)
    {
        public static ConnectResult Failure() => new(null, new IOException("Simulated connection failure."));
        public static ConnectResult Success(IHostMcpSession? session = null) => new(session ?? new TestMcpSession(McpPipeName.Format(5102)), null);
    }

    private sealed class FakeRetryClock : IRetryClock
    {
        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UnixEpoch;

        public Task DelayAsync(TimeSpan delay, CancellationToken ct)
        {
            Advance(delay);
            return Task.CompletedTask;
        }

        public void Advance(TimeSpan duration) => UtcNow += duration;
    }

    private sealed class TestMcpSession(string pipeName) : IHostMcpSession
    {
        public HostInstanceDescriptor Instance { get; } = new(5100, "Test", "1.0", pipeName);
        public bool IsConnected { get; private set; } = true;
        public event Action? CatalogChanged { add { } remove { } }
        public event Action? Disconnected;

        public Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct) => Task.FromResult<IList<McpClientTool>>([]);
        public Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct) => Task.FromResult<IList<McpClientPrompt>>([]);
        public Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct) => Task.FromResult<IList<McpClientResource>>([]);
        public Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct) => Task.FromResult<IList<McpClientResourceTemplate>>([]);
        public Task<CallToolResult> CallToolAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) => throw new NotSupportedException();
        public Task<GetPromptResult> GetPromptAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) => throw new NotSupportedException();
        public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct) => throw new NotSupportedException();

        public void Disconnect()
        {
            IsConnected = false;
            Disconnected?.Invoke();
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
