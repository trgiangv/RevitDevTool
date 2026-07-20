using DevTools.Daemon.Mcp;
using DevTools.Mcp;
using DevTools.Mcp.Routing;
using DevTools.Mcp.Routing.Catalog;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace RevitDevTool.Server.Tests;

public sealed class CatalogRefreshConcurrencyTests
{
    [Fact]
    public async Task FirstFetchWait_CompletesReadyForExactGeneration()
    {
        var session = new ReadinessSession(6101, 2, "DevTools_Revit_2025_6101");
        var sessions = new ReadinessInstanceManager(session);
        await using var coordinator = new HostCatalogCoordinator(_ => Task.CompletedTask, sessions);

        var wait = coordinator.WaitForFirstFetchAsync(
            6101,
            2,
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        coordinator.PublishStatus(new HostCatalogIdentity(session.Instance.PipeName, 1), HostCatalogState.Ready);
        Assert.False(wait.IsCompleted);

        coordinator.PublishStatus(new HostCatalogIdentity(session.Instance.PipeName, 2), HostCatalogState.Ready);

        Assert.Equal(HostCatalogState.Ready, await wait);
        Assert.Equal(
            HostCatalogState.Ready,
            await coordinator.WaitForFirstFetchAsync(
                6101,
                2,
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HostCatalogState.Stale)]
    [InlineData(HostCatalogState.Unavailable)]
    public async Task FirstFetchWait_CompletesForTerminalFirstState(HostCatalogState terminalState)
    {
        var session = new ReadinessSession(6105, 3, "terminal-state-pipe");
        var sessions = new ReadinessInstanceManager(session);
        await using var coordinator = new HostCatalogCoordinator(_ => Task.CompletedTask, sessions);
        var wait = coordinator.WaitForFirstFetchAsync(
            6105,
            3,
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        coordinator.PublishStatus(new HostCatalogIdentity(session.Instance.PipeName, 3), terminalState);

        Assert.Equal(terminalState, await wait);
    }

    [Fact]
    public async Task FirstFetchWait_ReturnsRefreshingWhenTimeoutExpires()
    {
        var session = new ReadinessSession(6102, 1, "custom-pipe-name");
        var sessions = new ReadinessInstanceManager(session);
        await using var coordinator = new HostCatalogCoordinator(_ => Task.CompletedTask, sessions);

        var state = await coordinator.WaitForFirstFetchAsync(
            6102,
            1,
            TimeSpan.FromMilliseconds(10),
            TestContext.Current.CancellationToken);

        Assert.Equal(HostCatalogState.Refreshing, state);
    }

    [Fact]
    public async Task FirstFetchWait_PropagatesCallerCancellation()
    {
        var session = new ReadinessSession(6103, 1, "another-custom-pipe");
        var sessions = new ReadinessInstanceManager(session);
        await using var coordinator = new HostCatalogCoordinator(_ => Task.CompletedTask, sessions);
        using var cancellation = new CancellationTokenSource();

        var wait = coordinator.WaitForFirstFetchAsync(6103, 1, TimeSpan.FromSeconds(10), cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }

    [Fact]
    public async Task FirstFetchWait_DisconnectCompletesUnavailableAndNewWaitReturnsUnavailable()
    {
        var session = new ReadinessSession(6104, 1, "disconnect-pipe");
        var sessions = new ReadinessInstanceManager(session);
        await using var coordinator = new HostCatalogCoordinator(_ => Task.CompletedTask, sessions);
        var wait = coordinator.WaitForFirstFetchAsync(
            6104,
            1,
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        sessions.Disconnect();
        coordinator.RequestRefresh();

        Assert.Equal(HostCatalogState.Unavailable, await wait);
        Assert.Equal(
            HostCatalogState.Unavailable,
            await coordinator.WaitForFirstFetchAsync(
                6104,
                1,
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FirstFetchWait_DisposeCannotMissWaiterRegistration()
    {
        var instanceRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseInstance = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new BlockingInstanceReadinessSession(6106, instanceRead, releaseInstance);
        var sessions = new BlockingInstanceManager(session);
        var coordinator = new HostCatalogCoordinator(_ => Task.CompletedTask, sessions);
        using var waiterCancellation = new CancellationTokenSource();
        var wait = Task.Run(() => coordinator.WaitForFirstFetchAsync(
            6106,
            1,
            Timeout.InfiniteTimeSpan,
            waiterCancellation.Token));

        await instanceRead.Task.WaitAsync(TestContext.Current.CancellationToken);
        await coordinator.DisposeAsync();
        releaseInstance.TrySetResult(true);

        try
        {
            Assert.Equal(
                HostCatalogState.Unavailable,
                await wait.WaitAsync(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken));
        }
        finally
        {
            waiterCancellation.Cancel();
        }
    }

    [Fact]
    public async Task OneHundredNotifications_CoalesceIntoAtMostTwoSerializedRebuilds()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maxConcurrent = 0;
        var maxConcurrentLock = new object();
        var total = 0;
        await using var coordinator = new HostCatalogCoordinator(async _ =>
        {
            var current = Interlocked.Increment(ref active);
            lock (maxConcurrentLock)
            {
                maxConcurrent = Math.Max(maxConcurrent, current);
            }

            Interlocked.Increment(ref total);
            entered.TrySetResult(true);
            await release.Task.ConfigureAwait(false);
            Interlocked.Decrement(ref active);
        });

        coordinator.RequestRefresh();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(Enumerable.Range(0, 99).Select(_ => Task.Run(coordinator.RequestRefresh)));
        release.TrySetResult(true);
        await coordinator.WaitForIdleAsync(TestContext.Current.CancellationToken);

        lock (maxConcurrentLock)
        {
            Assert.Equal(1, maxConcurrent);
        }

        Assert.InRange(total, 1, 2);
    }

    private sealed class ReadinessInstanceManager(params IHostMcpSession[] sessions) : IInstanceManager
    {
        private IReadOnlyCollection<IHostMcpSession> current = sessions;

        public IReadOnlyCollection<IHostMcpSession> Sessions => current;
        public event Action? SessionsChanged { add { } remove { } }

        public IHostMcpSession? GetSessionByProcessId(int processId) =>
            current.SingleOrDefault(session => session.Instance.ProcessId == processId && session.IsConnected);

        public IHostMcpSession? GetSession(int processId, int generation) =>
            GetSessionByProcessId(processId) is { Generation: var actual } session && actual == generation
                ? session
                : null;

        public void Disconnect() => current = [];
    }

    private sealed class ReadinessSession(int processId, int generation, string pipeName) : IHostMcpSession
    {
        public HostInstanceDescriptor Instance { get; } = new(processId, "TestHost", "1.0", pipeName);
        public int Generation { get; } = generation;
        public bool IsConnected => true;
        public event Action? CatalogChanged { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<CallToolResult> CallToolAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) => throw new NotSupportedException();
        public Task<GetPromptResult> GetPromptAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) => throw new NotSupportedException();
        public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingInstanceReadinessSession(
        int processId,
        TaskCompletionSource<bool> instanceRead,
        TaskCompletionSource<bool> releaseInstance) : IHostMcpSession
    {
        private readonly HostInstanceDescriptor instance = new(processId, "TestHost", "1.0", "dispose-race-pipe");

        public HostInstanceDescriptor Instance
        {
            get
            {
                instanceRead.TrySetResult(true);
                releaseInstance.Task.GetAwaiter().GetResult();
                return instance;
            }
        }

        public int Generation => 1;
        public bool IsConnected => true;
        public event Action? CatalogChanged { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<CallToolResult> CallToolAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) => throw new NotSupportedException();
        public Task<GetPromptResult> GetPromptAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) => throw new NotSupportedException();
        public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingInstanceManager(IHostMcpSession session) : IInstanceManager
    {
        public IReadOnlyCollection<IHostMcpSession> Sessions => [session];
        public event Action? SessionsChanged { add { } remove { } }
        public IHostMcpSession? GetSessionByProcessId(int processId) => processId == 6106 ? session : null;
        public IHostMcpSession? GetSession(int processId, int generation) =>
            processId == 6106 && generation == 1 ? session : null;
    }
}
