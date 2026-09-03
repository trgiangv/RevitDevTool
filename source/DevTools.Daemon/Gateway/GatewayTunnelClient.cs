using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DevTools.Mcp.Client;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ZLogger;

namespace DevTools.Daemon.Gateway;

internal sealed class GatewayTunnelClient(
    Uri gatewayUri,
    Func<Task<string?>> tokenProvider,
    McpServerOptions serverOptions,
    IMcpPipeScanner pipeScanner,
    ILoggerFactory loggerFactory,
    IServiceProvider appServices,
    ILogger logger) : IAsyncDisposable
{
    private const int ReconnectBaseDelayMs = 1_000;
    private const int ReconnectMaxDelayMs = 15_000;
    private const int MaxMessageSize = 4 * 1024 * 1024;
    private const int HeartbeatIntervalMs = 30_000;
    private const string RegisterMessageType = "register";
    private const string HeartbeatMessageType = "heartbeat";
    private const string AuthorizationHeader = "Authorization";
    private const string TransportName = "GatewayTunnel";
    private const string CloseDescription = "Shutdown";

    private ClientWebSocket? _ws;
    private bool _hasConnectedBefore;

    private TunnelStatus Status { get; set; } = TunnelStatus.Disconnected;
    public event EventHandler<TunnelStatusChangedArgs>? StatusChanged;

    private void SetStatus(TunnelStatus status)
    {
        if (Status == status) return;
        Status = status;
        StatusChanged?.Invoke(this, new TunnelStatusChangedArgs(status));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var delay = ReconnectBaseDelayMs;

        while (!ct.IsCancellationRequested)
        {
            SetStatus(_hasConnectedBefore ? TunnelStatus.Reconnecting : TunnelStatus.Connecting);

            try
            {
                await ConnectAndServeAsync(ct).ConfigureAwait(false);
                delay = ReconnectBaseDelayMs;
                continue;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                SetStatus(TunnelStatus.Disconnected);
                return;
            }
            catch (Exception ex)
            {
                logger.ZLogWarning(ex, $"Tunnel lost, retrying in {delay}ms...");
                SetStatus(TunnelStatus.Reconnecting);
            }

            try { await Task.Delay(delay, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                SetStatus(TunnelStatus.Disconnected);
                return;
            }

            delay = Math.Min(delay * 2, ReconnectMaxDelayMs);
        }
    }

    private async Task ConnectAndServeAsync(CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

        var currentToken = await tokenProvider().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(currentToken))
            ws.Options.SetRequestHeader(AuthorizationHeader, $"Bearer {currentToken}");

        _ws = ws;

        try
        {
            logger.ZLogInformation($"Connecting to gateway {gatewayUri}...");
            await ws.ConnectAsync(gatewayUri, ct).ConfigureAwait(false);
            logger.ZLogInformation($"Connected to gateway");
            _hasConnectedBefore = true;

            await SendRegisterAsync(ws, pipeScanner, ct).ConfigureAwait(false);
            SetStatus(TunnelStatus.Connected);

            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var heartbeatTask = HeartbeatLoopAsync(ws, pipeScanner, heartbeatCts.Token);

            var inbound = new WebSocketReadStream(ws, MaxMessageSize);
            var outbound = new WebSocketWriteStream(ws, logger);

            await using var server = McpServer.Create(
                new StreamServerTransport(inbound, outbound, TransportName, loggerFactory),
                serverOptions,
                loggerFactory,
                appServices);

            try
            {
                await server.RunAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                await heartbeatCts.CancelAsync().ConfigureAwait(false);
                try { await heartbeatTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
        }
        finally
        {
            if (ReferenceEquals(_ws, ws)) _ws = null;
        }
    }

    private static async Task SendRegisterAsync(ClientWebSocket ws, IMcpPipeScanner pipeScanner, CancellationToken ct)
    {
        var metadata = DeviceMetadata.Collect();
        var hostApps = pipeScanner.Discover();

        var register = new GatewayRegisterMessage(
            RegisterMessageType,
            metadata.MachineId,
            metadata.MachineName,
            hostApps.ToList());

        var json = JsonSerializer.Serialize(register);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
    }

    private static async Task HeartbeatLoopAsync(ClientWebSocket ws, IMcpPipeScanner pipeScanner, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            await Task.Delay(HeartbeatIntervalMs, ct).ConfigureAwait(false);

            var hostApps = pipeScanner.Discover();
            var heartbeat = new GatewayHeartbeatMessage(HeartbeatMessageType, hostApps.ToList());
            var json = JsonSerializer.Serialize(heartbeat);
            var bytes = Encoding.UTF8.GetBytes(json);

            try
            {
                await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
            }
            catch { break; }
        }
    }

    public async ValueTask DisposeAsync()
    {
        var ws = _ws;
        if (ws is { State: WebSocketState.Open })
        {
            try
            {
                await ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, CloseDescription, CancellationToken.None).ConfigureAwait(false);
            }
            catch { /* best-effort */ }
        }
        ws?.Dispose();
        _ws = null;
    }
}
