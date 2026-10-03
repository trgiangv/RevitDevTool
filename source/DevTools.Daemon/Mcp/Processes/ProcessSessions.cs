using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Daemon.Mcp.Processes;

/// <summary>Owns connected process MCP sessions and the in-memory <see cref="ProcessCatalogs"/>.</summary>
public sealed class ProcessSessions(
    IMcpPipeScanner pipeScanner,
    ILogger<ProcessSessions> logger,
    ILoggerFactory loggerFactory) : IProcessSessions, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ProcessSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _publishedPipes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownPipes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _knownPipesLock = new();

    public ProcessCatalogs Catalog { get; } = new();
    public event Action? Changed;

    public IProcessSession? GetByProcessId(int processId) =>
        _sessions.Values.FirstOrDefault(session => session.ProcessId == processId);

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SyncPipesAsync(ct).ConfigureAwait(false);
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

    private async Task SyncPipesAsync(CancellationToken ct)
    {
        var currentPipes = pipeScanner.Discover().ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<string> vanishedPipes;
        lock (_knownPipesLock)
            vanishedPipes = _knownPipes.Where(pipe => !currentPipes.Contains(pipe)).ToList();

        foreach (var pipeName in vanishedPipes)
            await DisconnectAsync(pipeName).ConfigureAwait(false);

        foreach (var pair in _sessions.ToArray())
        {
            if (pair.Value.IsConnected)
                continue;

            await DisconnectSessionAsync(pair.Value).ConfigureAwait(false);
        }

        List<string> newPipes;
        lock (_knownPipesLock)
            newPipes = currentPipes.Where(pipe => !_knownPipes.Contains(pipe)).ToList();

        foreach (var pipeName in newPipes)
        {
            if (!await TryConnectAsync(pipeName, ct).ConfigureAwait(false)) continue;
            lock (_knownPipesLock)
                _knownPipes.Add(pipeName);
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
            var session = await ProcessSession.ConnectAsync(pipeName, loggerFactory, logger, ct).ConfigureAwait(false);

            session.Disconnected += () => _ = DisconnectSessionAsync(session);
            session.CatalogChanged += () => _ = RefreshCatalogAsync(session, CancellationToken.None);

            _sessions[pipeName] = session;
            await RefreshCatalogAsync(session, ct).ConfigureAwait(false);

            logger.ZLogInformation($"Connected MCP to {pipeName} (PID={session.ProcessId}, Host={session.Info.HostApp})");
            Changed?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Failed to connect MCP to {pipeName}");
            return false;
        }
    }

    private async Task RefreshCatalogAsync(ProcessSession session, CancellationToken ct)
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
            if (!IsCurrentSession(session))
                return;

            var stopwatch = Stopwatch.StartNew();
            var tools = await session.Client.ListToolsAsync(cancellationToken: ct).ConfigureAwait(false);
            var resources = await session.Client.ListResourcesAsync(cancellationToken: ct).ConfigureAwait(false);
            var templates = await session.Client.ListResourceTemplatesAsync(cancellationToken: ct).ConfigureAwait(false);
            stopwatch.Stop();

            if (!IsCurrentSession(session))
                return;

            var entry = new ProcessCatalog
            {
                ProcessId = session.ProcessId,
                Instance = session.Info,
                PipeName = session.PipeName,
                Tools = tools.Select(tool => tool.ProtocolTool).ToArray(),
                Resources = resources.Select(resource => resource.ProtocolResource).ToArray(),
                ResourceTemplates = templates.Select(template => template.ProtocolResourceTemplate).ToArray()
            };

            Catalog.Replace(entry);

            using (logger.BeginScope(new Dictionary<string, object?>
            {
                ["hostPid"] = session.ProcessId,
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

    private bool IsCurrentSession(ProcessSession session) =>
        session.IsConnected &&
        _sessions.TryGetValue(session.PipeName, out var current) &&
        ReferenceEquals(current, session);

    private Task DisconnectAsync(string pipeName)
    {
        lock (_knownPipesLock)
            _knownPipes.Remove(pipeName);

        return _sessions.TryGetValue(pipeName, out var session)
            ? DisconnectSessionAsync(session)
            : Task.CompletedTask;
    }

    private async Task DisconnectSessionAsync(ProcessSession session)
    {
        var pipeName = session.PipeName;
        if (!_sessions.TryRemove(new KeyValuePair<string, ProcessSession>(pipeName, session)))
            return;

        lock (_knownPipesLock)
            _knownPipes.Remove(pipeName);

        Catalog.Remove(session.ProcessId);
        await session.DisposeAsync().ConfigureAwait(false);
        logger.ZLogInformation($"Disconnected MCP from {pipeName}");
        Changed?.Invoke();
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
