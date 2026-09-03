using System.Collections.Concurrent;
using System.Diagnostics;
using DevTools.Mcp.Core.Sessions;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Mcp.Client;

/// <summary>Owns connected host MCP sessions and the in-memory <see cref="ConnectedHostCatalog"/>.</summary>
public sealed class HostBroker(
    IMcpPipeScanner pipeScanner,
    ILogger<HostBroker> logger,
    ILoggerFactory loggerFactory) : IHostBroker, IHostDiscovery, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, HostSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly DeviceMetadata _device = DeviceMetadata.Collect();
    private readonly HashSet<string> _publishedPipes = new(StringComparer.OrdinalIgnoreCase);

    public IConnectedHostCatalog Catalog { get; } = new ConnectedHostCatalog();
    public event Action? Changed;

    public IHostSession? GetByProcessId(int processId) =>
        _sessions.Values.FirstOrDefault(session => session.Info.ProcessId == processId);

    public IHostSession? GetByHostKey(HostKey key) =>
        _sessions.Values.FirstOrDefault(session => session.Key.Equals(key));

    public async Task RunAsync(CancellationToken ct)
    {
        var knownPipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SyncPipesAsync(knownPipes, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.ZLogError(ex, $"MCP discovery error");
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
        var currentPipes = pipeScanner.Discover().ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var pipeName in knownPipes.Where(pipe => !currentPipes.Contains(pipe)).ToList())
        {
            knownPipes.Remove(pipeName);
            await DisconnectAsync(pipeName).ConfigureAwait(false);
        }

        foreach (var pipeName in currentPipes.Where(pipe => !knownPipes.Contains(pipe)).ToList())
        {
            if (await TryConnectAsync(pipeName, ct).ConfigureAwait(false))
                knownPipes.Add(pipeName);
        }

        if (!_publishedPipes.SetEquals(currentPipes))
        {
            _publishedPipes.Clear();
            foreach (var pipe in currentPipes)
                _publishedPipes.Add(pipe);
            Changed?.Invoke();
        }
    }

    private async Task<bool> TryConnectAsync(string pipeName, CancellationToken ct)
    {
        try
        {
            logger.ZLogInformation($"Connecting MCP client to {pipeName}...");
            var session = await HostSession.ConnectAsync(pipeName, _device.MachineId, loggerFactory, logger, ct).ConfigureAwait(false);

            session.Disconnected += () => _ = DisconnectAsync(pipeName);
            session.CatalogChanged += () => _ = RefreshCatalogAsync(session, CancellationToken.None);

            _sessions[pipeName] = session;
            await RefreshCatalogAsync(session, ct).ConfigureAwait(false);

            logger.ZLogInformation($"Connected MCP to {pipeName} (PID={session.Info.ProcessId}, Host={session.Info.HostApp})");
            Changed?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Failed to connect MCP to {pipeName}");
            return false;
        }
    }

    private async Task RefreshCatalogAsync(HostSession session, CancellationToken ct)
    {
        try
        {
            await session.CatalogRefreshGate.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }

        try
        {
            // A queued notification may outlive a disconnected/replaced pipe session.
            // Never publish its older snapshot into the current catalog.
            if (!IsCurrentSession(session))
                return;

            var stopwatch = Stopwatch.StartNew();
            var tools = await session.Client.ListToolsAsync(cancellationToken: ct).ConfigureAwait(false);
            var resources = await session.Client.ListResourcesAsync(cancellationToken: ct).ConfigureAwait(false);
            var templates = await session.Client.ListResourceTemplatesAsync(cancellationToken: ct).ConfigureAwait(false);
            stopwatch.Stop();

            if (!IsCurrentSession(session))
                return;

            var entry = new HostCatalogEntry
            {
                Key = session.Key,
                Instance = session.Info,
                PipeName = session.PipeName,
                Tools = tools.Select(tool => tool.ProtocolTool).ToArray(),
                Resources = resources.Select(resource => resource.ProtocolResource).ToArray(),
                ResourceTemplates = templates.Select(template => template.ProtocolResourceTemplate).ToArray()
            };

            Catalog.Replace(entry);

            using (logger.BeginScope(new Dictionary<string, object?>
            {
                ["machineId"] = session.Key.MachineId,
                ["hostPid"] = session.Info.ProcessId,
                ["pipeName"] = session.PipeName,
                ["durationMs"] = stopwatch.ElapsedMilliseconds,
                ["toolCount"] = entry.Tools.Count,
                ["resourceCount"] = entry.Resources.Count,
                ["templateCount"] = entry.ResourceTemplates.Count
            }))
            {
                logger.ZLogInformation($"Host catalog refreshed");
            }

            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Failed to refresh host catalog for {session.PipeName}");
        }
        finally
        {
            session.CatalogRefreshGate.Release();
        }
    }

    private bool IsCurrentSession(HostSession session) =>
        session.IsConnected &&
        _sessions.TryGetValue(session.PipeName, out var current) &&
        ReferenceEquals(current, session);

    private async Task DisconnectAsync(string pipeName)
    {
        if (_sessions.TryRemove(pipeName, out var session))
        {
            Catalog.Remove(session.Key);
            await session.DisposeAsync().ConfigureAwait(false);
            logger.ZLogInformation($"Disconnected MCP from {pipeName}");
            Changed?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var pair in _sessions.ToArray())
        {
            if (_sessions.TryRemove(pair.Key, out var session))
                await session.DisposeAsync().ConfigureAwait(false);
        }

        Catalog.Clear();
    }
}
