using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using DevTools.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Execution.External;

/// <summary>
/// Host-side pytest/control pipe server over <c>DevTools_{Host}_{Version}_{PID}</c>
/// (length-prefixed <see cref="BridgeMessage"/>). MCP uses a separate host pipe server.
/// </summary>
[UsedImplicitly]
public sealed class DevToolsPipeServer(
    IHostAppInfo hostInfo,
    IEnumerable<IBridgeRequestHandler> handlers,
    ILogger<DevToolsPipeServer> logger) : IHostedService, IDisposable
{
    private Dictionary<string, IBridgeRequestHandler> HandlerMap =>
        field ??= BuildHandlerMap();

    private Dictionary<string, IBridgeRequestHandler> BuildHandlerMap()
        => handlers.SelectMany(h => h.SupportedMethods.Select(m => (method: m, handler: h)))
            .ToDictionary(x => x.method, x => x.handler, StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private readonly ConcurrentDictionary<int, ConnectionEntry> _connections = new();
    private int _nextConnectionId;
    private string? _pipeName;
    private bool _disposed;

    private sealed class ConnectionEntry(BridgePipeConnection connection, CancellationTokenSource requestCts)
    {
        public BridgePipeConnection Connection { get; } = connection;
        public CancellationTokenSource RequestCts { get; } = requestCts;
        public int InFlight;
        public int Removed;
        private int _ctsDisposed;

        public void DisposeRequestCts()
        {
            if (Interlocked.Exchange(ref _ctsDisposed, 1) != 0)
                return;

            RequestCts.Dispose();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null) return Task.CompletedTask;
        _pipeName = HostPipeName.FormatTest(hostInfo.Host.ToString(), hostInfo.VersionNumber, Environment.ProcessId);

        var notificationPublishers = handlers.OfType<IBridgeNotificationPublisher>().ToList();
        foreach (var publisher in notificationPublishers)
            publisher.NotificationSender = SendNotification;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoopTask = AcceptLoopAsync(_cts.Token);

#if DEBUG
        logger.ZLogInformation($"Listening on pipe '{_pipeName}'.");
#endif
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.CancelAsync();
        foreach (var entry in _connections.Values)
        {
            try { await entry.RequestCts.CancelAsync().ConfigureAwait(false); } catch { /* best effort */ }
            entry.Connection.Dispose();
            entry.RequestCts.Dispose();
        }
        _connections.Clear();

        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
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
                RegisterConnection(pipe);
            }
            catch (OperationCanceledException) { break; }
            catch (IOException ex) when (NamedPipes.IsBusy(ex))
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"Accept loop error: {ex.Message}");
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
    }

    private void RegisterConnection(NamedPipeServerStream pipe)
    {
        var conn = new BridgePipeConnection(pipe);
        var requestCts = CancellationTokenSource.CreateLinkedTokenSource(_cts!.Token);
        var connectionId = Interlocked.Increment(ref _nextConnectionId);
        var entry = new ConnectionEntry(conn, requestCts);
        _connections[connectionId] = entry;
#if DEBUG
        logger.ZLogInformation($"Client connected. Active clients: {_connections.Count}");
#endif

        // Disconnect cancels in-flight work but must not dispose the CTS while
        // testing/run is still on the host thread (breakpoint / idle marshal).
        conn.MessageReceived += msg => OnMessageReceived(entry, msg);
        conn.Disconnected += () => OnDisconnected(connectionId);
        conn.StartReadLoop();
    }

    private void OnDisconnected(int connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var entry))
            return;

        Interlocked.Exchange(ref entry.Removed, 1);
        try { entry.RequestCts.Cancel(); } catch { /* best effort */ }
        entry.Connection.Dispose();
        if (Volatile.Read(ref entry.InFlight) == 0)
            entry.DisposeRequestCts();

#if DEBUG
        logger.ZLogInformation($"Client disconnected. Active clients: {_connections.Count}");
#endif
    }

    private async void OnMessageReceived(ConnectionEntry entry, BridgeMessage msg)
    {
        Interlocked.Increment(ref entry.InFlight);
        var requestCt = entry.RequestCts.Token;
        try
        {
            if (msg is not { Type: BridgeMessage.TypeRequest, Id: not null, Method: not null })
                return;

            BridgeMessage response;
            try
            {
                response = await HandleRequestAsync(msg, requestCt).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (requestCt.IsCancellationRequested)
            {
                response = BridgeMessage.Error(
                    msg.Id!,
                    IpcErrorCodes.InternalError,
                    "Request cancelled because the client disconnected.");
            }
            catch (Exception ex)
            {
                response = BridgeMessage.Error(msg.Id!, IpcErrorCodes.InternalError, ex.Message);
            }

            try
            {
                if (!requestCt.IsCancellationRequested)
                    await entry.Connection.WriteAsync(response, requestCt).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"Failed to send response: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError($"Unhandled error in message handler: {ex}");
        }
        finally
        {
            if (Interlocked.Decrement(ref entry.InFlight) == 0
                && Volatile.Read(ref entry.Removed) != 0)
                entry.DisposeRequestCts();
        }
    }

    private async Task<BridgeMessage> HandleRequestAsync(BridgeMessage request, CancellationToken ct)
    {
        var id = request.Id!;
        if (HandlerMap.TryGetValue(request.Method!, out var handler))
            return await handler.HandleAsync(id, request.Method!, request.Params, ct).ConfigureAwait(false);
        return BridgeMessage.Error(id, IpcErrorCodes.MethodNotFound, $"Unknown method: {request.Method}");
    }

    private async void SendNotification(string method, JsonElement? data = null)
    {
        try
        {
            if (_connections.IsEmpty) return;

            var notification = BridgeMessage.Notification(method, data);

            foreach (var entry in _connections.Values)
            {
                try
                {
                    await entry.Connection.WriteAsync(notification).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.ZLogWarning($"Notification '{method}' failed: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError($"Unhandled error in SendNotification: {ex}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        foreach (var entry in _connections.Values)
        {
            try { entry.RequestCts.Cancel(); } catch { /* best effort */ }
            entry.Connection.Dispose();
            entry.RequestCts.Dispose();
        }
        _connections.Clear();
        _cts?.Dispose();
    }
}
