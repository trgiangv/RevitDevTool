using System.Collections.Concurrent;
using System.Text.RegularExpressions;
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
    IHostMcpSession? Session);

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
    private readonly ConcurrentDictionary<string, HostBridgeClient> clients = new(StringComparer.OrdinalIgnoreCase);

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

    [GeneratedRegex(@"^DevTools_\w+_[^_]+_\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex HostPipePattern();

    public event Action? Changed;
    public event Action? SessionsChanged;

    public IReadOnlyCollection<IHostMcpSession> Sessions => sessionSlots.Values
        .Where(slot => slot.State == HostSessionState.Connected && slot.Session is { IsConnected: true })
        .Select(slot => slot.Session!)
        .ToArray();

    internal IReadOnlyCollection<HostSessionSlot> SessionSlots => sessionSlots.Values.ToArray();

    public IHostMcpSession? GetSessionByProcessId(int processId) =>
        Sessions.FirstOrDefault(session => session.Instance.ProcessId == processId);

    public List<HostBridgeClient> GetClients() => clients.Values.ToList();

    public IReadOnlyCollection<InstanceInfo> GetInstances() => clients.Values
        .Where(client => client.Info is not null)
        .Select(client => client.Info!)
        .ToList();

    IHostBridgeClient? IInstanceManager.GetByProcessId(int processId) => GetByProcessId(processId);

    public HostBridgeClient? GetByProcessId(int processId) =>
        clients.Values.FirstOrDefault(client => client.Info?.ProcessId == processId);

    public string? GetPipeNameByProcessId(int processId) =>
        clients.FirstOrDefault(pair => pair.Value.Info?.ProcessId == processId).Key;

    IHostBridgeClient? IInstanceManager.GetDefault(string? hostApp) => GetDefault(hostApp);

    private HostBridgeClient? GetDefault(string? hostApp = null)
    {
        if (string.IsNullOrWhiteSpace(hostApp))
            return clients.Count == 1 ? clients.Values.First() : null;

        var matches = clients.Values
            .Where(client => client.Info is not null &&
                             string.Equals(client.Info.HostApp, hostApp, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public IReadOnlyCollection<string> GetDiscoveredPipeNames() => DiscoverHostPipes(logger).ToArray();

    public async Task RunAsync(CancellationToken ct)
    {
        var knownLegacyPipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SyncPipesAsync(knownLegacyPipes, ct).ConfigureAwait(false);
                await SyncMcpPipesAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.ZLogError(ex, $"Discovery error");
            }

            try
            {
                await retryClock.DelayAsync(DiscoveryInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task SyncPipesAsync(HashSet<string> knownPipes, CancellationToken ct)
    {
        var currentPipes = DiscoverHostPipes(logger);
        foreach (var pipeName in knownPipes.Where(pipe => !currentPipes.Contains(pipe)).ToList())
        {
            knownPipes.Remove(pipeName);
            await DisconnectAsync(pipeName).ConfigureAwait(false);
        }

        foreach (var pipeName in currentPipes.Where(pipe => !knownPipes.Contains(pipe)).ToList())
        {
            knownPipes.Add(pipeName);
            await TryConnectAsync(pipeName, ct).ConfigureAwait(false);
        }
    }

    private async Task TryConnectAsync(string pipeName, CancellationToken ct)
    {
        try
        {
            logger.ZLogInformation($"Connecting to {pipeName}...");
            var client = await HostBridgeClient.ConnectAsync(pipeName, ct).ConfigureAwait(false);
            client.ToolsChanged += () => Changed?.Invoke();
            client.Disconnected += () =>
            {
                _ = DisconnectAsync(pipeName);
                Changed?.Invoke();
            };
            clients[pipeName] = client;
            logger.ZLogInformation($"Connected to {pipeName} (PID={client.Info?.ProcessId}, Host={client.Info?.HostApp})");
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Failed to connect to {pipeName}");
        }
    }

    internal async Task SyncMcpPipesAsync(CancellationToken ct)
    {
        var currentPipes = discoverMcpPipes(logger);
        foreach (var pipeName in sessionSlots.Keys.Where(pipe => !currentPipes.Contains(pipe)).ToArray())
            await RemoveSessionSlotAsync(pipeName).ConfigureAwait(false);

        foreach (var pipeName in currentPipes)
        {
            var slot = sessionSlots.GetOrAdd(
                pipeName,
                name => new HostSessionSlot(name, HostSessionState.Discovered, 0, retryClock.UtcNow, null));

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
        sessionSlots[pipeName] = slot with { State = HostSessionState.Connecting, Session = null };
        try
        {
            logger.ZLogInformation($"Connecting to MCP endpoint {pipeName}...");
            var session = await sessionConnector.ConnectAsync(pipeName, ct).ConfigureAwait(false);
            session.CatalogChanged += NotifySessionsChanged;
            session.Disconnected += () => _ = HandleSessionDisconnectedAsync(pipeName, session);
            sessionSlots[pipeName] = new HostSessionSlot(
                pipeName,
                HostSessionState.Connected,
                0,
                retryClock.UtcNow,
                session);
            logger.ZLogInformation($"Connected to MCP endpoint {pipeName} (PID={session.Instance.ProcessId}, Host={session.Instance.HostApp})");
            NotifySessionsChanged();
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Failed to connect to MCP endpoint {pipeName}");
            var failureCount = slot.FailureCount + 1;
            sessionSlots[pipeName] = new HostSessionSlot(
                pipeName,
                HostSessionState.Backoff,
                failureCount,
                retryClock.UtcNow + GetRetryDelay(failureCount),
                null);
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
        sessionSlots[pipeName] = new HostSessionSlot(
            pipeName,
            HostSessionState.Backoff,
            failureCount,
            retryClock.UtcNow + GetRetryDelay(failureCount),
            null);
        if (slot.Session is not null)
            await slot.Session.DisposeAsync().ConfigureAwait(false);
        NotifySessionsChanged();
    }

    private async Task RemoveSessionSlotAsync(string pipeName)
    {
        if (!sessionSlots.TryRemove(pipeName, out var slot))
            return;

        if (slot.Session is not null)
        {
            await slot.Session.DisposeAsync().ConfigureAwait(false);
            NotifySessionsChanged();
        }
    }

    private static TimeSpan GetRetryDelay(int failureCount) =>
        TimeSpan.FromMilliseconds(Math.Min(250d * Math.Pow(2, failureCount), MaximumRetryDelay.TotalMilliseconds));

    private async Task DisconnectAsync(string pipeName)
    {
        if (clients.TryRemove(pipeName, out var client))
        {
            await client.DisposeAsync().ConfigureAwait(false);
            logger.ZLogInformation($"Disconnected from {pipeName}");
        }
    }

    public static HashSet<string> DiscoverHostPipes(ILogger? logger = null)
    {
        var pipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var path in Directory.GetFiles(@"\\.\pipe\"))
            {
                var name = Path.GetFileName(path);
                if (IsHostEntryPipe(name))
                    pipes.Add(name);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.ZLogWarning(ex, $"Pipe scan error");
        }
        return pipes;
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

    private static bool IsHostEntryPipe(string name) => HostPipePattern().IsMatch(name);

    private void NotifySessionsChanged() => SessionsChanged?.Invoke();

    public async ValueTask DisposeAsync()
    {
        foreach (var pair in clients.ToArray())
        {
            if (clients.TryRemove(pair.Key, out var client))
                await client.DisposeAsync().ConfigureAwait(false);
        }

        foreach (var pipeName in sessionSlots.Keys.ToArray())
            await RemoveSessionSlotAsync(pipeName).ConfigureAwait(false);
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
