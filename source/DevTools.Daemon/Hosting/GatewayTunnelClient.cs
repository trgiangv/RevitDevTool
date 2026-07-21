using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DevTools.Daemon.Mcp;
using DevTools.Ipc;
using DevTools.Mcp.Routing;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ZLogger;

namespace DevTools.Daemon.Hosting;

/// <summary>One v2 WebSocket carrier which multiplexes independent logical MCP sessions.</summary>
public sealed class GatewayTunnelClient(
    Uri gatewayUri,
    Func<Task<string?>> tokenProvider,
    Func<McpServerOptions> optionsFactory,
    IInstanceManager sessions,
    ILoggerFactory loggerFactory,
    IServiceProvider services,
    ILogger logger) : IAsyncDisposable
{
    private const int ReconnectBaseDelayMs = 1_000;
    private const int ReconnectMaxDelayMs = 15_000;
    private const int MaxMessageSize = 4 * 1024 * 1024;
    private const int HeartbeatIntervalMs = 30_000;
    private const string AuthorizationHeader = "Authorization";
    public const string BearerScheme = "Bearer";
    private const string CloseDescription = "Shutdown";

    private readonly SemaphoreSlim sendLock = new(1, 1);
    private ClientWebSocket? webSocket;
    private bool hasConnectedBefore;

    private TunnelStatus Status { get; set; } = TunnelStatus.Disconnected;
    public event EventHandler<TunnelStatusChangedArgs>? StatusChanged;

    private void SetStatus(TunnelStatus status)
    {
        if (Status == status) return;
        Status = status;
        StatusChanged?.Invoke(this, new TunnelStatusChangedArgs(status));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var delay = ReconnectBaseDelayMs;
        while (!cancellationToken.IsCancellationRequested)
        {
            SetStatus(hasConnectedBefore ? TunnelStatus.Reconnecting : TunnelStatus.Connecting);
            try
            {
                await ConnectAndServeAsync(cancellationToken).ConfigureAwait(false);
                delay = ReconnectBaseDelayMs;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SetStatus(TunnelStatus.Disconnected);
                return;
            }
            catch (Exception exception)
            {
                logger.ZLogWarning(exception, $"Tunnel lost, retrying in {delay}ms...");
                SetStatus(TunnelStatus.Reconnecting);
            }

            try { await Task.Delay(delay, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SetStatus(TunnelStatus.Disconnected);
                return;
            }
            delay = Math.Min(delay * 2, ReconnectMaxDelayMs);
        }
    }

    private async Task ConnectAndServeAsync(CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        var token = await tokenProvider().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(token))
            socket.Options.SetRequestHeader(AuthorizationHeader, $"{BearerScheme} {token}");
        webSocket = socket;

        try
        {
            logger.ZLogInformation($"Connecting to gateway {gatewayUri}...");
            await socket.ConnectAsync(gatewayUri, cancellationToken).ConfigureAwait(false);
            hasConnectedBefore = true;

            await SendEnvelopeAsync(socket, CreateRegisterEnvelope(), cancellationToken).ConfigureAwait(false);
            GatewayTunnelEnvelope? registered;
            do { registered = await ReceiveEnvelopeAsync(socket, cancellationToken).ConfigureAwait(false); }
            while (registered is null);
            if (registered.Type != GatewayTunnelEnvelope.Registered)
                throw new InvalidDataException("Gateway did not acknowledge v2 tunnel registration.");

            if (!ProtocolCompatibility.IsAtLeast(registered.GatewayVersion, ProtocolCompatibility.MinGatewayVersion))
            {
                throw new ProtocolCompatibilityException(
                    "gateway_version_mismatch",
                    ProtocolCompatibility.FormatMismatch("gateway", registered.GatewayVersion, ProtocolCompatibility.MinGatewayVersion));
            }

            SetStatus(TunnelStatus.Connected);
            await using var manager = new GatewaySessionManager(optionsFactory, loggerFactory, services);
            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var heartbeat = HeartbeatLoopAsync(socket, heartbeatCts.Token);
            try
            {
                while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    var envelope = await ReceiveEnvelopeAsync(socket, cancellationToken).ConfigureAwait(false);
                    if (envelope is not null)
                        await DispatchAsync(manager, socket, envelope, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                await heartbeatCts.CancelAsync().ConfigureAwait(false);
                try { await heartbeat.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
        }
        finally
        {
            if (ReferenceEquals(webSocket, socket)) webSocket = null;
        }
    }

    private async Task DispatchAsync(
        GatewaySessionManager manager,
        ClientWebSocket socket,
        GatewayTunnelEnvelope envelope,
        CancellationToken cancellationToken)
    {
        switch (envelope.Type)
        {
            case GatewayTunnelEnvelope.SessionOpen:
                try
                {
                    await manager.OpenAsync(envelope.SessionId!, (outbound, ct) => SendEnvelopeAsync(socket, outbound, ct), cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    await SendEnvelopeAsync(socket, GatewayTunnelEnvelope.Closed(envelope.SessionId!, GatewayTunnelEnvelope.UnknownSession), cancellationToken).ConfigureAwait(false);
                }
                break;
            case GatewayTunnelEnvelope.McpMessage:
                if (!await manager.RouteAsync(envelope.SessionId!, envelope.Message!.Value, cancellationToken).ConfigureAwait(false))
                    await SendEnvelopeAsync(socket, GatewayTunnelEnvelope.Closed(envelope.SessionId!, GatewayTunnelEnvelope.UnknownSession), cancellationToken).ConfigureAwait(false);
                break;
            case GatewayTunnelEnvelope.SessionClose:
                await manager.CloseAsync(envelope.SessionId!, envelope.Reason ?? GatewayTunnelEnvelope.UnknownSession, cancellationToken).ConfigureAwait(false);
                break;
            default:
                logger.ZLogWarning($"Ignoring unexpected tunnel envelope type {envelope.Type}");
                break;
        }
    }

    private GatewayTunnelEnvelope CreateRegisterEnvelope()
    {
        var metadata = DeviceMetadata.Collect();
        var daemonVersion = typeof(GatewayTunnelClient).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        return new GatewayTunnelEnvelope(
            GatewayTunnelEnvelope.ProtocolVersion,
            GatewayTunnelEnvelope.Register,
            MachineId: metadata.MachineId,
            MachineName: metadata.MachineName,
            HostApps: GetHostApps(),
            DaemonVersion: daemonVersion);
    }

    private async Task HeartbeatLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            await Task.Delay(HeartbeatIntervalMs, cancellationToken).ConfigureAwait(false);
            await SendEnvelopeAsync(socket, new GatewayTunnelEnvelope(
                GatewayTunnelEnvelope.ProtocolVersion, GatewayTunnelEnvelope.Heartbeat, HostApps: GetHostApps()), cancellationToken).ConfigureAwait(false);
        }
    }

    private IReadOnlyList<string> GetHostApps() => sessions.Sessions
        .Where(session => session.IsConnected)
        .Select(session => $"{session.Instance.HostApp}_{session.Instance.VersionNumber}_{session.Instance.ProcessId}")
        .Order(StringComparer.Ordinal)
        .ToArray();

    private async ValueTask SendEnvelopeAsync(ClientWebSocket socket, GatewayTunnelEnvelope envelope, CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
        await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false); }
        finally { sendLock.Release(); }
    }

    private async Task<GatewayTunnelEnvelope?> ReceiveEnvelopeAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var bytes = new byte[64 * 1024];
        using var payload = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("Gateway tunnel closed.");
            if (result.MessageType != WebSocketMessageType.Text) throw new InvalidDataException("Gateway tunnel requires text envelopes.");
            if (payload.Length + result.Count > MaxMessageSize) throw new InvalidDataException("Gateway tunnel envelope exceeded the maximum size.");
            payload.Write(bytes, 0, result.Count);
        } while (!result.EndOfMessage);

        try
        {
            using var document = JsonDocument.Parse(payload.ToArray());
            if (GatewayTunnelEnvelope.TryParse(document.RootElement, out var envelope, out _))
                return envelope;
        }
        catch (JsonException) { }

        logger.ZLogWarning($"Dropping malformed gateway tunnel envelope");
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        var socket = webSocket;
        if (socket is { State: WebSocketState.Open })
        {
            try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, CloseDescription, CancellationToken.None).ConfigureAwait(false); }
            catch { }
        }
        socket?.Dispose();
        webSocket = null;
        sendLock.Dispose();
    }
}
