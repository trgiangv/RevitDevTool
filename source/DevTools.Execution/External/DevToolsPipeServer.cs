using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using DevTools.Hosting;
using DevTools.Execution.External.Connections;
using DevTools.Mcp.Adapter.External;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Execution.External;

/// <summary>
/// Host-side pytest/control pipe server over <c>DevTools_{Host}_{Version}_{PID}</c>
/// (length-prefixed <see cref="BridgeMessage"/>). MCP uses <see cref="HostMcpPipeServer"/>.
/// </summary>
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
    private readonly ConcurrentDictionary<int, ConnectionEntry> _connections = new();
    private int _nextConnectionId;
    private string? _pipeName;
    private bool _disposed;

    private sealed class ConnectionEntry(BridgePipeConnection connection, CancellationTokenSource requestCts)
    {
        public BridgePipeConnection Connection { get; } = connection;
        public CancellationTokenSource RequestCts { get; } = requestCts;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null) return Task.CompletedTask;
        _pipeName = HostPipeName.FormatPytest(hostInfo.Host.ToString(), hostInfo.VersionNumber, Environment.ProcessId);
        state.SetEndpoint(_pipeName);
        state.SetConnectedState(0);
        state.SetQueueDepth(0);

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
        _connections[connectionId] = new ConnectionEntry(conn, requestCts);
        state.SetConnectedState(_connections.IsEmpty ? 0 : 1);
#if DEBUG
        logger.ZLogInformation($"Client connected. Active clients: {_connections.Count}");
#endif

        // Disconnect must cancel the connection token so host handlers (NUnit run, etc.)
        // stop instead of holding the Revit executor after Runner/adapter kill.
        conn.MessageReceived += msg => OnMessageReceived(conn, requestCts.Token, msg);
        conn.Disconnected += () =>
        {
            try { requestCts.Cancel(); } catch { /* best effort */ }
            if (_connections.TryRemove(connectionId, out var entry))
            {
                entry.Connection.Dispose();
                entry.RequestCts.Dispose();
            }

            state.SetConnectedState(_connections.IsEmpty ? 0 : 1);
#if DEBUG
            logger.ZLogInformation($"Client disconnected. Active clients: {_connections.Count}");
#endif
        };
        conn.StartReadLoop();
    }

    private async void OnMessageReceived(
        BridgePipeConnection connection,
        CancellationToken requestCt,
        BridgeMessage msg)
    {
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
                    await connection.WriteAsync(response, requestCt).ConfigureAwait(false);
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
