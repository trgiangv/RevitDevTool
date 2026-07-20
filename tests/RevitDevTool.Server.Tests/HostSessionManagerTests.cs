using DevTools.Daemon.Mcp;
using DevTools.Mcp.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace RevitDevTool.Server.Tests;

public sealed class HostSessionManagerTests
{
    [Fact]
    public void Discovery_PassesVendorPrefixAndRejectsMalformedPid()
    {
        string? pattern = null;

        var names = HostSessionManager.DiscoverHostPipesForTest(value =>
        {
            pattern = value;
            return ["DevTools_Revit_2025_17", "DevTools_Revit_2025_zero"];
        });

        Assert.Equal("DevTools_*", pattern);
        Assert.Equal(["DevTools_Revit_2025_17"], names);
    }

    [Fact]
    public async Task ProcessIndex_TracksConnectDisconnectAndReconnectGeneration()
    {
        var pipeName = PipeName(42001);
        var connector = new FakeConnector(
            pipeName,
            [ConnectResult.Success(), ConnectResult.Success()]);
        var clock = new FakeRetryClock();
        await using var manager = CreateManager(pipeName, connector, clock);

        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);
        var first = Assert.IsType<TestMcpSession>(manager.GetSession(42001, 1));
        Assert.Same(first, manager.GetSession(42001, 1));
        Assert.Equal([1], connector.RequestedGenerations);

        first.Disconnect();
        await Task.Yield();
        clock.Advance(TimeSpan.FromSeconds(1));
        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);

        Assert.Null(manager.GetSession(42001, 1));
        Assert.Equal(2, manager.GetSession(42001, 2)!.Generation);
        Assert.Equal([1, 2], connector.RequestedGenerations);
    }

    [Fact]
    public async Task RediscoveredPipe_AssignsNextGeneration()
    {
        var pipeName = PipeName(42003);
        var discoveredPipes = new HashSet<string>([pipeName], StringComparer.OrdinalIgnoreCase);
        var connector = new FakeConnector(pipeName, [ConnectResult.Success(), ConnectResult.Success()]);
        var clock = new FakeRetryClock();
        await using var manager = CreateManager(discoveredPipes, connector, clock);

        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);
        discoveredPipes.Clear();
        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);
        discoveredPipes.Add(pipeName);
        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], connector.RequestedGenerations);
        Assert.Null(manager.GetSession(42003, 1));
        Assert.Equal(2, manager.GetSession(42003, 2)!.Generation);
    }

    [Fact]
    public async Task DuplicateLivePid_IsRejected()
    {
        var firstPipeName = PipeName(42001);
        var secondPipeName = PipeName(42002);
        var connector = new DelegateConnector((_, generation, _) =>
            Task.FromResult<IHostMcpSession>(new TestMcpSession(firstPipeName, generation)));
        var clock = new FakeRetryClock();
        await using var manager = CreateManager(
            new HashSet<string>([firstPipeName, secondPipeName], StringComparer.OrdinalIgnoreCase),
            connector,
            clock);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SlotRemovalDuringPidPublication_DoesNotAnnounceRemovedSession()
    {
        var pipeName = PipeName(42004);
        var discoveredPipes = new HashSet<string>([pipeName], StringComparer.OrdinalIgnoreCase);
        HostSessionManager? manager = null;
        Task? removal = null;
        var session = new TestMcpSession(
            pipeName,
            generation: 1,
            onFirstInstanceAccess: () =>
            {
                discoveredPipes.Clear();
                removal = manager!.SyncMcpPipesAsync(TestContext.Current.CancellationToken);
            });
        var connector = new FakeConnector(pipeName, [ConnectResult.Success(session)]);
        var clock = new FakeRetryClock();
        manager = CreateManager(discoveredPipes, connector, clock);
        await using var ownedManager = manager;
        var sessionChanges = 0;
        manager.SessionsChanged += () => sessionChanges++;

        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);
        await removal!;

        Assert.Empty(manager.Sessions);
        Assert.Equal(1, sessionChanges);
    }

    [Fact]
    public async Task ConnectorInvalidOperationException_TransitionsToBackoff()
    {
        var pipeName = PipeName(42005);
        var connector = new FakeConnector(pipeName, [ConnectResult.InvalidOperationFailure()]);
        var clock = new FakeRetryClock();
        await using var manager = CreateManager(pipeName, connector, clock);

        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HostSessionState.Backoff, Assert.Single(manager.SessionSlots).State);
    }

    [Fact]
    public async Task Discovered_ConnectsToConnectedSession()
    {
        var pipeName = PipeName(5101);
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
        var pipeName = PipeName(5102);
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
        var pipeName = PipeName(5103);
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
        var pipeName = PipeName(5104);
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

    [Fact]
    public async Task PendingConnect_RemovedPipeDoesNotPublishOrLeakSession()
    {
        var pipeName = PipeName(5105);
        var discoveredPipes = new HashSet<string>([pipeName], StringComparer.OrdinalIgnoreCase);
        var connector = new PendingConnector();
        var clock = new FakeRetryClock();
        await using var manager = CreateManager(discoveredPipes, connector, clock);

        var connect = manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);
        await connector.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        discoveredPipes.Clear();
        var removal = manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);
        Assert.False(removal.IsCompleted);
        var session = new TestMcpSession(pipeName);
        connector.Complete(session);
        await removal;
        await connect;

        Assert.Empty(manager.SessionSlots);
        Assert.Empty(manager.Sessions);
        Assert.Equal(1, session.DisposeCount);
    }

    [Fact]
    public async Task PendingConnect_ManagerShutdownCancelsAndAwaitsAttempt()
    {
        var pipeName = PipeName(5106);
        var connector = new PendingConnector(cancelWhenRequested: true);
        var clock = new FakeRetryClock();
        var manager = CreateManager(pipeName, connector, clock);

        var connect = manager.SyncMcpPipesAsync(CancellationToken.None);
        await connector.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        await manager.DisposeAsync();
        await connect;

        Assert.True(connector.CancellationRequested);
        Assert.Empty(manager.SessionSlots);
        Assert.Empty(manager.Sessions);
    }

    [Fact]
    public async Task PendingConnect_StaleCompletionIsDisposedDuringShutdown()
    {
        var pipeName = PipeName(5107);
        var connector = new PendingConnector();
        var clock = new FakeRetryClock();
        var manager = CreateManager(pipeName, connector, clock);

        var connect = manager.SyncMcpPipesAsync(CancellationToken.None);
        await connector.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var dispose = manager.DisposeAsync().AsTask();
        Assert.False(dispose.IsCompleted);

        var session = new TestMcpSession(pipeName);
        connector.Complete(session);
        await dispose;
        await connect;

        Assert.Empty(manager.SessionSlots);
        Assert.Equal(1, session.DisposeCount);
    }

    [Fact]
    public async Task RunAsync_UsesExactFirstRetryDeadline()
    {
        var pipeName = PipeName(5108);
        using var stop = new CancellationTokenSource();
        var connector = new FakeConnector(pipeName, [ConnectResult.Failure(), ConnectResult.Success()]);
        var clock = new RecordingRetryClock(stop, stopAfterDelays: 2);
        await using var manager = CreateManager(pipeName, connector, clock);

        await manager.RunAsync(stop.Token);

        Assert.Equal(TimeSpan.FromMilliseconds(500), clock.Delays[0]);
        Assert.Equal(2, connector.AttemptsFor(pipeName));
    }

    [Fact]
    public async Task RunAsync_CapsRetryDeadlineAtFifteenSeconds()
    {
        var pipeName = PipeName(5109);
        using var stop = new CancellationTokenSource();
        var clock = new RecordingRetryClock(stop, stopAfterDelays: int.MaxValue);
        var attempts = new List<DateTimeOffset>();
        var connector = new FakeConnector(
            pipeName,
            Enumerable.Repeat(ConnectResult.Failure(), 6),
            () =>
            {
                attempts.Add(clock.UtcNow);
                if (attempts.Count == 6)
                    stop.Cancel();
            });
        await using var manager = CreateManager(pipeName, connector, clock);

        await manager.RunAsync(stop.Token);

        Assert.Equal(
        [
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMilliseconds(500),
            DateTimeOffset.UnixEpoch.AddSeconds(1.5),
            DateTimeOffset.UnixEpoch.AddSeconds(3.5),
            DateTimeOffset.UnixEpoch.AddSeconds(7.5),
            DateTimeOffset.UnixEpoch.AddSeconds(15.5)
        ],
        attempts);
    }

    [Fact]
    public async Task RunAsync_DisconnectDuringDiscoverySleepRetriesAtBackoffDeadline()
    {
        var pipeName = PipeName(5111);
        using var stop = new CancellationTokenSource();
        var firstSession = new TestMcpSession(pipeName);
        var connectorAttempts = 0;
        var connector = new FakeConnector(
            pipeName,
            [ConnectResult.Success(firstSession), ConnectResult.Success()],
            () =>
            {
                if (connectorAttempts++ == 1)
                    stop.Cancel();
            });
        var clock = new ControllableRetryClock();
        await using var manager = CreateManager(pipeName, connector, clock);

        var run = manager.RunAsync(stop.Token);
        await clock.WaitForDelayCountAsync(1, TestContext.Current.CancellationToken);

        firstSession.Disconnect();
        await clock.WaitForDelayCountAsync(2, TestContext.Current.CancellationToken, TimeSpan.FromSeconds(1));

        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(500)], clock.Delays);

        clock.CompleteDelay(1);
        await run;

        Assert.Equal(2, connector.AttemptsFor(pipeName));
    }

    [Fact]
    public async Task CancelledConnect_PropagatesCancellationWithoutBackoffState()
    {
        var pipeName = PipeName(5110);
        using var cancellation = new CancellationTokenSource();
        var connector = new PendingConnector(cancelWhenRequested: true);
        var clock = new FakeRetryClock();
        await using var manager = CreateManager(pipeName, connector, clock);

        var connect = manager.SyncMcpPipesAsync(cancellation.Token);
        await connector.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connect);
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

    private sealed class FakeConnector(
        string expectedPipeName,
        IEnumerable<ConnectResult> results,
        Action? onAttempt = null) : IHostSessionConnector
    {
        private readonly Queue<ConnectResult> _results = new(results);
        private readonly Dictionary<string, int> _attempts = new(StringComparer.OrdinalIgnoreCase);
        public List<int> RequestedGenerations { get; } = [];

        public Task<IHostMcpSession> ConnectAsync(string pipeName, int generation, CancellationToken ct)
        {
            Assert.Equal(expectedPipeName, pipeName);
            _attempts[pipeName] = AttemptsFor(pipeName) + 1;
            RequestedGenerations.Add(generation);
            onAttempt?.Invoke();
            var result = _results.Dequeue();
            return result.Exception is { } exception
                ? Task.FromException<IHostMcpSession>(exception)
                : Task.FromResult<IHostMcpSession>(result.Session ?? new TestMcpSession(pipeName, generation));
        }

        public int AttemptsFor(string pipeName) => _attempts.GetValueOrDefault(pipeName);
    }

    private sealed record ConnectResult(IHostMcpSession? Session, Exception? Exception)
    {
        public static ConnectResult Failure() => new(null, new IOException("Simulated connection failure."));
        public static ConnectResult InvalidOperationFailure() => new(null, new InvalidOperationException("Simulated connector failure."));
        public static ConnectResult Success(IHostMcpSession? session = null) => new(session, null);
    }

    private sealed class DelegateConnector(
        Func<string, int, CancellationToken, Task<IHostMcpSession>> connectAsync) : IHostSessionConnector
    {
        public Task<IHostMcpSession> ConnectAsync(string pipeName, int generation, CancellationToken ct) =>
            connectAsync(pipeName, generation, ct);
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

    private sealed class RecordingRetryClock(CancellationTokenSource stop, int stopAfterDelays) : IRetryClock
    {
        private int _delayCount;

        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UnixEpoch;
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken ct)
        {
            Delays.Add(delay);
            UtcNow += delay;
            if (Interlocked.Increment(ref _delayCount) >= stopAfterDelays)
                stop.Cancel();
            return Task.CompletedTask;
        }
    }

    private sealed class ControllableRetryClock : IRetryClock
    {
        private readonly List<TaskCompletionSource<bool>> completions = [];
        private readonly SemaphoreSlim delayAdded = new(0);

        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UnixEpoch;
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken ct)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (completions)
            {
                Delays.Add(delay);
                completions.Add(completion);
                delayAdded.Release();
            }

            ct.Register(() => completion.TrySetCanceled(ct));
            return completion.Task;
        }

        public async Task WaitForDelayCountAsync(int count, CancellationToken ct, TimeSpan? timeout = null)
        {
            using var waitTimeout = timeout is { } duration
                ? CancellationTokenSource.CreateLinkedTokenSource(ct)
                : null;
            waitTimeout?.CancelAfter(timeout!.Value);
            var waitToken = waitTimeout?.Token ?? ct;
            while (true)
            {
                lock (completions)
                {
                    if (completions.Count >= count)
                        return;
                }

                await delayAdded.WaitAsync(waitToken).ConfigureAwait(false);
            }
        }

        public void CompleteDelay(int index)
        {
            TaskCompletionSource<bool> completion;
            TimeSpan delay;
            lock (completions)
            {
                completion = completions[index];
                delay = Delays[index];
            }

            UtcNow += delay;
            completion.TrySetResult(true);
        }
    }

    private sealed class PendingConnector(bool cancelWhenRequested = false) : IHostSessionConnector
    {
        private readonly TaskCompletionSource<IHostMcpSession> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CancellationRequested { get; private set; }

        public Task<IHostMcpSession> ConnectAsync(string pipeName, int generation, CancellationToken ct)
        {
            Entered.TrySetResult(true);
            if (cancelWhenRequested)
                ct.Register(() =>
                {
                    CancellationRequested = true;
                    _completion.TrySetCanceled(ct);
                });
            return _completion.Task;
        }

        public void Complete(IHostMcpSession session) => _completion.TrySetResult(session);
    }

    private sealed class TestMcpSession(
        string pipeName,
        int generation = 1,
        Action? onFirstInstanceAccess = null) : IHostMcpSession
    {
        private readonly HostInstanceDescriptor instance = new(GetProcessId(pipeName), "Test", "1.0", pipeName);
        private int instanceAccessed;

        public HostInstanceDescriptor Instance
        {
            get
            {
                if (Interlocked.Exchange(ref instanceAccessed, 1) == 0)
                    onFirstInstanceAccess?.Invoke();
                return instance;
            }
        }
        public int Generation { get; } = generation;
        public bool IsConnected { get; private set; } = true;
        public int DisposeCount { get; private set; }
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
            DisposeCount++;
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        private static int GetProcessId(string pipeName)
        {
            Assert.True(HostPipeName.TryParse(pipeName, out _, out _, out var processId));
            return processId;
        }
    }

    private static string PipeName(int processId) =>
        HostPipeName.Format("TestHost", "1.0", processId);
}
