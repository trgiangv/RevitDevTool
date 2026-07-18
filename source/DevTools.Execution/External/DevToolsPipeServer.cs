using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using DevTools.Logging;
using DevTools.Execution.External.Connections;
using DevTools.Execution.External.Handlers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Execution.External;

[UsedImplicitly]
public sealed class DevToolsPipeServer(
    ConnectionState state,
    IHostAppInfo hostInfo,
    IEnumerable<IBridgeRequestHandler> handlers,
    ILogger<DevToolsPipeServer> logger) : IHostedService, IDisposable
{
    private const int MaxPipeInstances = 8;

    private Dictionary<string, IBridgeRequestHandler> HandlerMap =>
        field ??= BuildHandlerMap();

    private Dictionary<string, IBridgeRequestHandler> BuildHandlerMap()
        => handlers.SelectMany(h => h.SupportedMethods.Select(m => (method: m, handler: h)))
            .ToDictionary(x => x.method, x => x.handler, StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private readonly ConcurrentDictionary<int, BridgePipeConnection> _connections = new();
    private int _nextConnectionId;
    private string? _pipeName;
    private bool _disposed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null) return Task.CompletedTask;
        _pipeName = HostPipeName.Format(hostInfo.Host.ToString(), hostInfo.VersionNumber, Environment.ProcessId);
        state.SetEndpoint(_pipeName);
        state.SetConnectedState(0);
        state.SetQueueDepth(0);

        var pytestHandler = handlers.OfType<PytestRequestHandler>().SingleOrDefault();
        if (pytestHandler is not null)
            pytestHandler.NotifySender = SendNotification;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoopTask = AcceptLoopAsync(_cts.Token);

        logger.ZLogInformation($"Listening on pipe '{_pipeName}'.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.CancelAsync();
        foreach (var connection in _connections.Values)
            connection.Dispose();
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

        state.SetConnectedState(0);
        state.SetQueueDepth(0);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pipe = CurrentUserPipeFactory.CreateDuplexServer(_pipeName!, MaxPipeInstances);
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                RegisterConnection(pipe);
            }
            catch (OperationCanceledException) { break; }
            catch (IOException ex) when (IsPipeInstancesBusy(ex))
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
        var connectionId = Interlocked.Increment(ref _nextConnectionId);
        _connections[connectionId] = conn;
        state.SetConnectedState(_connections.IsEmpty ? 0 : 1);
        logger.ZLogInformation($"Client connected. Active clients: {_connections.Count}");

        conn.MessageReceived += msg => OnMessageReceived(conn, msg);
        conn.Disconnected += () =>
        {
            if (_connections.TryRemove(connectionId, out var disconnectedConnection))
                disconnectedConnection.Dispose();
            state.SetConnectedState(_connections.IsEmpty ? 0 : 1);
            logger.ZLogInformation($"Client disconnected. Active clients: {_connections.Count}");
        };
        conn.StartReadLoop();
    }

    private async void OnMessageReceived(BridgePipeConnection connection, BridgeMessage msg)
    {
        try
        {
            if (msg is not { Type: BridgeMessage.TypeRequest, Id: not null, Method: not null })
                return;

            BridgeMessage response;
            try
            {
                response = await HandleRequestAsync(msg).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                response = BridgeMessage.Error(msg.Id!, PytestBridgeMethods.InternalError, ex.Message);
            }

            try
            {
                await connection.WriteAsync(response).ConfigureAwait(false);
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
    }

    private async Task<BridgeMessage> HandleRequestAsync(BridgeMessage request)
    {
        var id = request.Id!;
        if (HandlerMap.TryGetValue(request.Method!, out var handler))
            return await handler.HandleAsync(id, request.Method!, request.Params).ConfigureAwait(false);
        return BridgeMessage.Error(id, PytestBridgeMethods.MethodNotFound, $"Unknown method: {request.Method}");
    }

    private async void SendNotification(string method, JsonElement? data = null)
    {
        try
        {
            if (_connections.IsEmpty) return;

            var notification = BridgeMessage.Notification(method, data);

            foreach (var connection in _connections.Values)
            {
                try
                {
                    await connection.WriteAsync(notification).ConfigureAwait(false);
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

    private static bool IsPipeInstancesBusy(IOException ex)
    {
        const int allPipeInstancesBusy = 231;
        var win32Code = ex.HResult & 0xFFFF;
        return win32Code == allPipeInstancesBusy ||
               ex.Message.Contains("All pipe instances are busy", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        foreach (var connection in _connections.Values)
            connection.Dispose();
        _connections.Clear();
        _cts?.Dispose();
    }
}
