using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZLogger;

namespace DevTools.Daemon.Mcp;

public sealed partial class InstanceManager : IInstanceManager, IAsyncDisposable
{
    private readonly ILogger<InstanceManager> logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly Func<ILogger?, HashSet<string>> discoverMcpPipes;
    private readonly Func<string, CancellationToken, Task<IHostMcpSession>> connectMcpSessionAsync;

    public InstanceManager(
        ILogger<InstanceManager> logger,
        ILoggerFactory loggerFactory)
        : this(
            logger,
            loggerFactory,
            DiscoverMcpPipes,
            async (pipeName, ct) =>
                await HostMcpSession.ConnectAsync(pipeName, loggerFactory, ct).ConfigureAwait(false))
    {
    }

    public InstanceManager(ILogger<InstanceManager> logger)
        : this(logger, NullLoggerFactory.Instance)
    {
    }

    internal InstanceManager(
        ILogger<InstanceManager> logger,
        ILoggerFactory loggerFactory,
        Func<ILogger?, HashSet<string>> discoverMcpPipes,
        Func<string, CancellationToken, Task<IHostMcpSession>> connectMcpSessionAsync)
    {
        this.logger = logger;
        this.loggerFactory = loggerFactory;
        this.discoverMcpPipes = discoverMcpPipes;
        this.connectMcpSessionAsync = connectMcpSessionAsync;
    }

    /// <summary>
    /// Matches pipe names produced by DevToolsPipeServer: DevTools_{HostApp}_{Version}_{PID}.
    /// Host is any word chars, version is flexible (year, semver, etc.), PID is digits.
    /// </summary>
    [GeneratedRegex(@"^DevTools_\w+_[^_]+_\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex HostPipePattern();

    private readonly ConcurrentDictionary<string, HostBridgeClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IHostMcpSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;
    public event Action? SessionsChanged;

    public IReadOnlyCollection<IHostMcpSession> Sessions => _sessions.Values.ToArray();

    public IHostMcpSession? GetSessionByProcessId(int processId) =>
        _sessions.Values.FirstOrDefault(session => session.Instance.ProcessId == processId);

    public List<HostBridgeClient> GetClients() => _clients.Values.ToList();

    public IReadOnlyCollection<InstanceInfo> GetInstances() =>
        _clients.Values
            .Where(c => c.Info is not null)
            .Select(c => c.Info!)
            .ToList();

    IHostBridgeClient? IInstanceManager.GetByProcessId(int processId) => GetByProcessId(processId);

    public HostBridgeClient? GetByProcessId(int processId) =>
        _clients.Values.FirstOrDefault(c => c.Info?.ProcessId == processId);

    public string? GetPipeNameByProcessId(int processId) =>
        _clients.FirstOrDefault(kvp => kvp.Value.Info?.ProcessId == processId).Key;

    IHostBridgeClient? IInstanceManager.GetDefault(string? hostApp) => GetDefault(hostApp);

    private HostBridgeClient? GetDefault(string? hostApp = null)
    {
        if (string.IsNullOrWhiteSpace(hostApp))
            return _clients.Count == 1 ? _clients.Values.First() : null;

        var matches = _clients.Values
            .Where(c => c.Info is not null &&
                        string.Equals(c.Info.HostApp, hostApp, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matches.Length == 1 ? matches[0] : null;
    }

    public IReadOnlyCollection<string> GetDiscoveredPipeNames() =>
        DiscoverHostPipes(logger).ToArray();

    public async Task RunDiscoveryAsync(CancellationToken ct)
    {
        var knownPipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownMcpPipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SyncPipesAsync(knownPipes, ct).ConfigureAwait(false);
                await SyncMcpPipesAsync(knownMcpPipes, ct).ConfigureAwait(false);
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
                await Task.Delay(2000, ct).ConfigureAwait(false);
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

        foreach (var pipeName in knownPipes.Where(p => !currentPipes.Contains(p)).ToList())
        {
            knownPipes.Remove(pipeName);
            await DisconnectAsync(pipeName).ConfigureAwait(false);
        }

        foreach (var pipeName in currentPipes.Where(p => !knownPipes.Contains(p)).ToList())
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

            _clients[pipeName] = client;
            logger.ZLogInformation($"Connected to {pipeName} (PID={client.Info?.ProcessId}, Host={client.Info?.HostApp})");

            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Failed to connect to {pipeName}");
        }
    }

    internal async Task SyncMcpPipesAsync(HashSet<string> knownPipes, CancellationToken ct)
    {
        var currentPipes = discoverMcpPipes(logger);

        foreach (var pipeName in knownPipes.Where(pipe => !currentPipes.Contains(pipe)).ToList())
        {
            knownPipes.Remove(pipeName);
            await DisconnectMcpAsync(pipeName).ConfigureAwait(false);
        }

        foreach (var pipeName in currentPipes)
        {
            knownPipes.Add(pipeName);
            if (_sessions.TryGetValue(pipeName, out var session) && session.IsConnected)
                continue;

            if (session is not null)
                await DisconnectMcpAsync(pipeName).ConfigureAwait(false);

            await TryConnectMcpAsync(pipeName, ct).ConfigureAwait(false);
        }
    }

    private async Task TryConnectMcpAsync(string pipeName, CancellationToken ct)
    {
        try
        {
            logger.ZLogInformation($"Connecting to MCP endpoint {pipeName}...");
            var session = await connectMcpSessionAsync(pipeName, ct).ConfigureAwait(false);
            session.CatalogChanged += NotifySessionsChanged;
            session.Disconnected += () =>
            {
                _ = DisconnectMcpAsync(pipeName);
            };

            _sessions[pipeName] = session;
            logger.ZLogInformation(
                $"Connected to MCP endpoint {pipeName} (PID={session.Instance.ProcessId}, Host={session.Instance.HostApp})");
            NotifySessionsChanged();
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Failed to connect to MCP endpoint {pipeName}");
        }
    }

    private async Task DisconnectAsync(string pipeName)
    {
        if (_clients.TryRemove(pipeName, out var client))
        {
            await client.DisposeAsync().ConfigureAwait(false);
            logger.ZLogInformation($"Disconnected from {pipeName}");
        }
    }

    private async Task DisconnectMcpAsync(string pipeName)
    {
        if (_sessions.TryRemove(pipeName, out var session))
        {
            await session.DisposeAsync().ConfigureAwait(false);
            logger.ZLogInformation($"Disconnected from MCP endpoint {pipeName}");
            NotifySessionsChanged();
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

    private void NotifySessionsChanged()
    {
        SessionsChanged?.Invoke();
        Changed?.Invoke();
    }

    private static bool IsHostEntryPipe(string name) => HostPipePattern().IsMatch(name);

    public async ValueTask DisposeAsync()
    {
        foreach (var kvp in _clients.ToArray())
        {
            if (_clients.TryRemove(kvp.Key, out var client))
                await client.DisposeAsync().ConfigureAwait(false);
        }

        foreach (var kvp in _sessions.ToArray())
        {
            if (_sessions.TryRemove(kvp.Key, out var session))
                await session.DisposeAsync().ConfigureAwait(false);
        }
    }
}
