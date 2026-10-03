using System.Collections.Concurrent;
using System.IO.Pipes;
using DevTools.Execution.Abstractions;
using DevTools.Hosting;
using DevTools.Ipc;
using DevTools.Mcp.Hosting;
using DevTools.Mcp.Isolation;
using DevTools.Mcp.Core.Sessions;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ZLogger;

namespace DevTools.Mcp.Transport;

/// <summary>
/// MCP server over <c>DevToolsMcp_{Host}_{Version}_{PID}</c>.
/// <c>DevToolsPipeServer</c> remains the pytest and control pipe.
/// </summary>
[UsedImplicitly]
public sealed class McpPipeServer(
    McpCatalogStore catalogStore,
    IEnumerable<IMcpSource> sources,
    IConnectTracker connect,
    McpToolsetContextManager toolsetContextManager,
    IHostContextExecutor hostContext,
    IHostAppInfo hostInfo,
    IServiceProvider services,
    ILogger<McpPipeServer> logger) : IHostedService, IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private readonly ConcurrentDictionary<int, PipeEndpoint> _sessions = new();
    private int _nextSessionId;
    private int _lastToolCount;
    private string? _pipeName;
    private bool _disposed;

    private McpServerPrimitiveCollection<McpServerTool>? _tools;
    private McpServerResourceCollection? _resources;
    private McpServerOptions? _sharedOptions;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
            return Task.CompletedTask;

        _pipeName = HostPipeName.FormatMcp(hostInfo.Host.ToString(), hostInfo.VersionNumber, Environment.ProcessId);
        connect.SetEndpoint(_pipeName);

        Task.Run(() =>
        {
            try
            {
                catalogStore.EnsureLoaded();
                Interlocked.Exchange(ref _lastToolCount, catalogStore.GetToolDescriptors().Count);
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"MCP catalog preload failed: {ex.Message}");
            }
        }, cancellationToken);

        _sharedOptions = McpServerCollections.CreateOptions(
            catalogStore,
            sources,
            hostContext,
            services);
        _tools = _sharedOptions.ToolCollection!;
        _resources = _sharedOptions.ResourceCollection!;

        catalogStore.CatalogChanged += OnCatalogChanged;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoopTask = AcceptLoopAsync(_cts.Token);

#if DEBUG
        logger.ZLogInformation($"MCP listening on pipe '{_pipeName}' (SDK host server).");
#endif
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        catalogStore.CatalogChanged -= OnCatalogChanged;
        _cts?.Cancel();

        foreach (var session in _sessions.Values)
            await session.DisposeAsync().ConfigureAwait(false);
        _sessions.Clear();
        connect.Reset();

        if (_acceptLoopTask is not null)
        {
            try { await _acceptLoopTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _acceptLoopTask = null;
        _cts?.Dispose();
        _cts = null;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pipe = NamedPipes.Open(_pipeName!);
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                _ = HandleConnectionAsync(pipe, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (IOException ex) when (NamedPipes.IsBusy(ex))
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"MCP accept loop error: {ex.Message}");
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        if (_sharedOptions is null)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            return;
        }

        var sessionId = Interlocked.Increment(ref _nextSessionId);
        PipeEndpoint? endpoint = null;
        try
        {
            var options = CloneSharedOptions(_sharedOptions);
            endpoint = PipeEndpoint.Create(pipe, options, services, $"Mcp-{sessionId}");
            _sessions[sessionId] = endpoint;
            connect.SetClientCount(_sessions.Count);
#if DEBUG
            logger.ZLogInformation($"MCP client connected. Active sessions: {_sessions.Count}");
#endif
            await endpoint.RunAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.ZLogWarning($"MCP session ended: {ex.Message}");
        }
        finally
        {
            if (endpoint is not null)
            {
                _sessions.TryRemove(sessionId, out _);
                await endpoint.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }

#if DEBUG
            logger.ZLogInformation($"MCP client disconnected. Active sessions: {_sessions.Count}");
#endif
            connect.SetClientCount(_sessions.Count);
        }
    }

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        if (_tools is null || _resources is null)
            return;

        try
        {
            McpServerCollections.RebuildCollections(
                catalogStore,
                sources,
                hostContext,
                toolsetContextManager,
                _tools,
                _resources);

            var tools = catalogStore.GetToolDescriptors().Count;
            var resources = catalogStore.GetResourceDescriptors().Count;
            var previous = Interlocked.Exchange(ref _lastToolCount, tools);
            var added = tools - previous;
            if (added > 0)
            {
                logger.ZLogInformation(
                    $"MCP host catalog added {added} tool(s) ({tools} tools, {resources} resources).");
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"Failed to reload MCP host catalog");
        }
    }

    private static McpServerOptions CloneSharedOptions(McpServerOptions sharedOptions) =>
        new()
        {
            ServerInfo = sharedOptions.ServerInfo,
            ToolCollection = sharedOptions.ToolCollection,
            ResourceCollection = sharedOptions.ResourceCollection,
            Capabilities = sharedOptions.Capabilities,
            Filters = sharedOptions.Filters,
            Handlers = sharedOptions.Handlers,
            ServerInstructions = sharedOptions.ServerInstructions,
            ProtocolVersion = sharedOptions.ProtocolVersion
        };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        catalogStore.CatalogChanged -= OnCatalogChanged;
        _cts?.Cancel();
        foreach (var session in _sessions.Values)
            session.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _sessions.Clear();
        _cts?.Dispose();
    }
}
