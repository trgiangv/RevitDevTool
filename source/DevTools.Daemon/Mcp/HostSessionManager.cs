using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZLogger;

namespace DevTools.Daemon.Mcp;

internal interface IHostSessionConnector
{
    Task<IHostMcpSession> ConnectAsync(string pipeName, CancellationToken ct);
}

internal interface IRetryClock
{
    DateTimeOffset UtcNow { get; }
    Task DelayAsync(TimeSpan delay, CancellationToken ct);
}

internal sealed record HostSessionSlot(
    string PipeName,
    HostSessionState State,
    int FailureCount,
    DateTimeOffset RetryAt,
    IHostMcpSession? Session,
    int Generation = 0,
    SessionConnectionAttempt? Attempt = null);

internal sealed class SessionConnectionAttempt : IDisposable
{
    private readonly CancellationTokenSource cancellation;

    public SessionConnectionAttempt(int generation, CancellationToken cancellationToken)
    {
        Generation = generation;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    public int Generation { get; }
    public CancellationToken Token => cancellation.Token;
    public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Cancel()
    {
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose() => cancellation.Dispose();
}

internal enum HostSessionState
{
    Discovered,
    Connecting,
    Connected,
    Backoff
}

public sealed partial class HostSessionManager : IInstanceManager, IAsyncDisposable
{
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(15);
    private readonly ILogger<HostSessionManager> logger;
    private readonly Func<ILogger?, HashSet<string>> discoverMcpPipes;
    private readonly IHostSessionConnector sessionConnector;
    private readonly IRetryClock retryClock;
    private readonly ConcurrentDictionary<string, HostSessionSlot> sessionSlots = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly Lock discoveryWaitGate = new();
    private readonly Lock disposeGate = new();
    private CancellationTokenSource? discoveryWaitCancellation;
    private Task? disposeTask;

    public HostSessionManager(ILogger<HostSessionManager> logger, ILoggerFactory loggerFactory)
        : this(
            logger,
            loggerFactory,
            DiscoverMcpPipes,
            new HostMcpSessionConnector(loggerFactory),
            SystemRetryClock.Instance)
    {
    }

    public HostSessionManager(ILogger<HostSessionManager> logger)
        : this(logger, NullLoggerFactory.Instance)
    {
    }

    internal HostSessionManager(
        ILogger<HostSessionManager> logger,
        ILoggerFactory loggerFactory,
        Func<ILogger?, HashSet<string>> discoverMcpPipes,
        IHostSessionConnector sessionConnector,
        IRetryClock retryClock)
    {
        this.logger = logger;
        this.discoverMcpPipes = discoverMcpPipes;
        this.sessionConnector = sessionConnector;
        this.retryClock = retryClock;
    }

    public event Action? SessionsChanged;

    public IReadOnlyCollection<IHostMcpSession> Sessions => sessionSlots.Values
        .Where(slot => slot.State == HostSessionState.Connected && slot.Session is { IsConnected: true })
        .Select(slot => slot.Session!)
        .ToArray();

    internal IReadOnlyCollection<HostSessionSlot> SessionSlots => sessionSlots.Values.ToArray();

    public IHostMcpSession? GetSessionByProcessId(int processId) =>
        Sessions.FirstOrDefault(session => session.Instance.ProcessId == processId);

    public async Task RunAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetimeCancellation.Token);
        var token = linked.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await SyncMcpPipesAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.ZLogError(ex, $"Discovery error");
            }

            try
            {
                await DelayUntilNextDiscoveryAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task DelayUntilNextDiscoveryAsync(CancellationToken ct)
    {
        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lock (discoveryWaitGate)
            discoveryWaitCancellation = waitCancellation;

        try
        {
            await retryClock.DelayAsync(GetNextDiscoveryDelay(), waitCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (waitCancellation.IsCancellationRequested && !ct.IsCancellationRequested)
        {
        }
        finally
        {
            lock (discoveryWaitGate)
            {
                if (ReferenceEquals(discoveryWaitCancellation, waitCancellation))
                    discoveryWaitCancellation = null;
            }
        }
    }

    internal async Task SyncMcpPipesAsync(CancellationToken ct)
    {
        if (lifetimeCancellation.IsCancellationRequested)
            return;

        var currentPipes = discoverMcpPipes(logger);
        foreach (var pipeName in sessionSlots.Keys.Where(pipe => !currentPipes.Contains(pipe)).ToArray())
            await RemoveSessionSlotAsync(pipeName).ConfigureAwait(false);

        foreach (var pipeName in currentPipes)
        {
            var slot = sessionSlots.GetOrAdd(
                pipeName,
                name => new HostSessionSlot(name, HostSessionState.Discovered, 0, retryClock.UtcNow, null));

            if (slot.State == HostSessionState.Connecting)
                continue;

            if (slot.State == HostSessionState.Connected && slot.Session is { IsConnected: true })
                continue;

            if (slot.State == HostSessionState.Connected)
            {
                await MoveToBackoffAsync(pipeName, slot).ConfigureAwait(false);
                slot = sessionSlots[pipeName];
            }

            if (slot.State == HostSessionState.Backoff && slot.RetryAt > retryClock.UtcNow)
                continue;

            await ConnectSessionAsync(pipeName, slot, ct).ConfigureAwait(false);
        }
    }

    private async Task ConnectSessionAsync(string pipeName, HostSessionSlot slot, CancellationToken ct)
    {
        if (lifetimeCancellation.IsCancellationRequested)
            return;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetimeCancellation.Token);
        var attempt = new SessionConnectionAttempt(slot.Generation + 1, linked.Token);
        var connecting = slot with
        {
            State = HostSessionState.Connecting,
            Session = null,
            Generation = attempt.Generation,
            Attempt = attempt
        };
        if (!sessionSlots.TryUpdate(pipeName, connecting, slot))
        {
            attempt.Dispose();
            return;
        }

        try
        {
            logger.ZLogInformation($"Connecting to MCP endpoint {pipeName}...");
            var session = await sessionConnector.ConnectAsync(pipeName, attempt.Token).ConfigureAwait(false);
            var connected = new HostSessionSlot(
                pipeName,
                HostSessionState.Connected,
                0,
                retryClock.UtcNow,
                session);
            if (attempt.Token.IsCancellationRequested)
            {
                await session.DisposeAsync().ConfigureAwait(false);
                throw new OperationCanceledException(attempt.Token);
            }

            if (!sessionSlots.TryUpdate(pipeName, connected, connecting))
            {
                await session.DisposeAsync().ConfigureAwait(false);
                return;
            }

            session.CatalogChanged += NotifySessionsChanged;
            session.Disconnected += () => _ = HandleSessionDisconnectedAsync(pipeName, session);
            logger.ZLogInformation($"Connected to MCP endpoint {pipeName} (PID={session.Instance.ProcessId}, Host={session.Instance.HostApp})");
            NotifySessionsChanged();
        }
        catch (OperationCanceledException) when (attempt.Token.IsCancellationRequested)
        {
            RemoveAttemptIfCurrent(pipeName, connecting);
            if (ct.IsCancellationRequested)
                throw;
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Failed to connect to MCP endpoint {pipeName}");
            var failureCount = slot.FailureCount + 1;
            var backoff = new HostSessionSlot(
                pipeName,
                HostSessionState.Backoff,
                failureCount,
                retryClock.UtcNow + GetRetryDelay(failureCount),
                null);
            if (sessionSlots.TryUpdate(pipeName, backoff, connecting))
                WakeDiscoveryLoop();
        }
        finally
        {
            attempt.Completion.TrySetResult(true);
            attempt.Dispose();
        }
    }

    private async Task HandleSessionDisconnectedAsync(string pipeName, IHostMcpSession session)
    {
        if (!sessionSlots.TryGetValue(pipeName, out var slot) || !ReferenceEquals(slot.Session, session))
            return;

        await MoveToBackoffAsync(pipeName, slot).ConfigureAwait(false);
    }

    private async Task MoveToBackoffAsync(string pipeName, HostSessionSlot slot)
    {
        var failureCount = slot.FailureCount + 1;
        var backoff = new HostSessionSlot(
            pipeName,
            HostSessionState.Backoff,
            failureCount,
            retryClock.UtcNow + GetRetryDelay(failureCount),
            null);
        if (!sessionSlots.TryUpdate(pipeName, backoff, slot))
            return;
        WakeDiscoveryLoop();
        if (slot.Session is not null)
            await slot.Session.DisposeAsync().ConfigureAwait(false);
        NotifySessionsChanged();
    }

    private async Task RemoveSessionSlotAsync(string pipeName)
    {
        if (!sessionSlots.TryRemove(pipeName, out var slot))
            return;

        if (slot.Attempt is { } attempt)
        {
            attempt.Cancel();
            await attempt.Completion.Task.ConfigureAwait(false);
        }

        if (slot.Session is not null)
        {
            await slot.Session.DisposeAsync().ConfigureAwait(false);
            NotifySessionsChanged();
        }
    }

    private static TimeSpan GetRetryDelay(int failureCount) =>
        TimeSpan.FromMilliseconds(Math.Min(250d * Math.Pow(2, failureCount), MaximumRetryDelay.TotalMilliseconds));

    private TimeSpan GetNextDiscoveryDelay()
    {
        var now = retryClock.UtcNow;
        var earliestRetry = sessionSlots.Values
            .Where(slot => slot.State == HostSessionState.Backoff)
            .Select(slot => slot.RetryAt > now ? slot.RetryAt - now : TimeSpan.Zero)
            .DefaultIfEmpty(DiscoveryInterval)
            .Min();
        return earliestRetry < DiscoveryInterval ? earliestRetry : DiscoveryInterval;
    }

    private void WakeDiscoveryLoop()
    {
        lock (discoveryWaitGate)
            discoveryWaitCancellation?.Cancel();
    }

    private void RemoveAttemptIfCurrent(string pipeName, HostSessionSlot connecting)
    {
        if (sessionSlots.TryGetValue(pipeName, out var current) &&
            ReferenceEquals(current.Attempt, connecting.Attempt))
        {
            ((ICollection<KeyValuePair<string, HostSessionSlot>>)sessionSlots)
                .Remove(new KeyValuePair<string, HostSessionSlot>(pipeName, connecting));
        }
    }

    private static HashSet<string> DiscoverMcpPipes(ILogger? logger = null)
    {
        var pipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var path in Directory.GetFiles(@"\\.\pipe\"))
            {
                var name = Path.GetFileName(path);
                if (McpPipeName.TryParse(name, out _))
                    pipes.Add(name);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.ZLogWarning(ex, $"MCP pipe scan error");
        }
        return pipes;
    }

    private void NotifySessionsChanged() => SessionsChanged?.Invoke();

    public ValueTask DisposeAsync()
    {
        lock (disposeGate)
            return new ValueTask(disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        lifetimeCancellation.Cancel();

        foreach (var pipeName in sessionSlots.Keys.ToArray())
            await RemoveSessionSlotAsync(pipeName).ConfigureAwait(false);

        lifetimeCancellation.Dispose();
    }

    private sealed class HostMcpSessionConnector(ILoggerFactory loggerFactory) : IHostSessionConnector
    {
        public async Task<IHostMcpSession> ConnectAsync(string pipeName, CancellationToken ct) =>
            await HostMcpSession.ConnectAsync(pipeName, loggerFactory, ct).ConfigureAwait(false);
    }

    private sealed class SystemRetryClock : IRetryClock
    {
        public static SystemRetryClock Instance { get; } = new();
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public Task DelayAsync(TimeSpan delay, CancellationToken ct) => Task.Delay(delay, ct);
    }
}
