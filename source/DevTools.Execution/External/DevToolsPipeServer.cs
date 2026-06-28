using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
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
    McpCatalogStore catalogStore,
    ConnectionState state,
    IHostAppInfo hostInfo,
    IMcpPrimitiveDispatcher primitiveDispatcher,
    McpToolsetContextManager toolsetContextManager,
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
        _pipeName = $"{hostInfo.Host}_{hostInfo.VersionNumber}_{Environment.ProcessId}";
        state.SetEndpoint(_pipeName);
        state.SetConnectedState(0);
        state.SetQueueDepth(0);

        var pytestHandler = handlers.OfType<PytestRequestHandler>().SingleOrDefault();
        if (pytestHandler is not null)
            pytestHandler.NotifySender = SendNotification;

        Task.Run(() =>
        {
            try
            {
                catalogStore.EnsureLoaded();
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"[PipeServer] Catalog preload failed: {ex.Message}");
            }
        }, cancellationToken);

        catalogStore.CatalogChanged += OnCatalogChanged;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoopTask = AcceptLoopAsync(_cts.Token);

        logger.ZLogInformation($"[PipeServer] Listening on pipe '{_pipeName}'.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        catalogStore.CatalogChanged -= OnCatalogChanged;

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
                var pipe = CreateServerPipe(_pipeName!);
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
                logger.ZLogWarning($"[PipeServer] Accept loop error: {ex.Message}");
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
        logger.ZLogInformation($"[PipeServer] Client connected. Active clients: {_connections.Count}");

        conn.MessageReceived += msg => OnMessageReceived(conn, msg);
        conn.Disconnected += () =>
        {
            if (_connections.TryRemove(connectionId, out var disconnectedConnection))
                disconnectedConnection.Dispose();
            state.SetConnectedState(_connections.IsEmpty ? 0 : 1);
            logger.ZLogInformation($"[PipeServer] Client disconnected. Active clients: {_connections.Count}");
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
                response = BridgeMessage.Error(msg.Id!, IpcErrorCodes.InternalError, ex.Message);
            }

            try
            {
                await connection.WriteAsync(response).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"[PipeServer] Failed to send response: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError($"[PipeServer] Unhandled error in message handler: {ex}");
        }
    }

    private async Task<BridgeMessage> HandleRequestAsync(BridgeMessage request)
    {
        var id = request.Id!;
        if (HandlerMap.TryGetValue(request.Method!, out var handler))
            return await handler.HandleAsync(id, request.Method!, request.Params).ConfigureAwait(false);
        return BridgeMessage.Error(id, IpcErrorCodes.MethodNotFound, $"Unknown method: {request.Method}");
    }

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        primitiveDispatcher.ClearCaches();
        toolsetContextManager.Clear();
        SendNotification(McpBridgeMethods.NotifyToolsChanged);
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
                    logger.ZLogWarning($"[PipeServer] Notification '{method}' failed: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError($"[PipeServer] Unhandled error in SendNotification: {ex}");
        }
    }

    private static NamedPipeServerStream CreateServerPipe(string pipeName)
    {
        var security = new PipeSecurity();
        var currentUser = WindowsIdentity.GetCurrent();
        if (currentUser.User is null)
            throw new InvalidOperationException("Cannot determine current user SID for pipe ACL.");

        security.AddAccessRule(new PipeAccessRule(
            currentUser.User,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

#if NETFRAMEWORK
        return new NamedPipeServerStream(pipeName, PipeDirection.InOut, MaxPipeInstances,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
#else
        return NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, MaxPipeInstances,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
#endif
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
        catalogStore.CatalogChanged -= OnCatalogChanged;
        _cts?.Cancel();
        foreach (var connection in _connections.Values)
            connection.Dispose();
        _connections.Clear();
        _cts?.Dispose();
    }
}
