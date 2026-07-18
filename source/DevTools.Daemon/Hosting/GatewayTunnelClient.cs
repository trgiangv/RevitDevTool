using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DevTools.Daemon.Contracts;
using DevTools.Daemon.Mcp;
using DevTools.Mcp.Routing;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ZLogger;

namespace DevTools.Daemon.Hosting;

/// <summary>
/// Outbound WebSocket tunnel to a remote MCP gateway relay.
/// Each WebSocket text frame = exactly one JSON-RPC message (NDJSON over stream).
/// Auto-reconnects with exponential backoff on failure only.
/// </summary>
public sealed class GatewayTunnelClient(
    Uri gatewayUri,
    Func<Task<string?>> tokenProvider,
    McpServerOptions serverOptions,
    IInstanceManager sessions,
    ILoggerFactory loggerFactory,
    ILogger logger) : IAsyncDisposable
{
    private const int ReconnectBaseDelayMs = 1_000;
    private const int ReconnectMaxDelayMs = 15_000;
    private const int MaxMessageSize = 4 * 1024 * 1024;
    private const int HeartbeatIntervalMs = 30_000;
    private const string RegisterMessageType = "register";
    private const string HeartbeatMessageType = "heartbeat";
    private const string AuthorizationHeader = "Authorization";
    public const string BearerScheme = "Bearer";
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
            ws.Options.SetRequestHeader(AuthorizationHeader, $"{BearerScheme} {currentToken}");

        _ws = ws;

        try
        {
            logger.ZLogInformation($"Connecting to gateway {gatewayUri}...");
            await ws.ConnectAsync(gatewayUri, ct).ConfigureAwait(false);
            logger.ZLogInformation($"Connected to gateway");
            _hasConnectedBefore = true;

            await SendRegisterAsync(ws, ct).ConfigureAwait(false);
            SetStatus(TunnelStatus.Connected);

            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var heartbeatTask = HeartbeatLoopAsync(ws, heartbeatCts.Token);

            var inbound = new WebSocketReadStream(ws, MaxMessageSize);
            var outbound = new WebSocketWriteStream(ws, logger);

            await using var server = McpServer.Create(
                new StreamServerTransport(inbound, outbound, TransportName, loggerFactory),
                serverOptions);

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

    private async Task SendRegisterAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var metadata = DeviceMetadata.Collect();
        var hostApps = GetHostApps();

        var register = new GatewayRegisterMessage(
            RegisterMessageType,
            metadata.MachineId,
            metadata.MachineName,
            hostApps.ToList());

        var json = JsonSerializer.Serialize(register);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
    }

    private async Task HeartbeatLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            await Task.Delay(HeartbeatIntervalMs, ct).ConfigureAwait(false);

            var hostApps = GetHostApps();
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

    private IReadOnlyList<string> GetHostApps() => sessions.Sessions
        .Where(session => session.IsConnected)
        .Select(session => $"{session.Instance.HostApp}_{session.Instance.VersionNumber}_{session.Instance.ProcessId}")
        .Order(StringComparer.Ordinal)
        .ToArray();

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

    /// <summary>
    /// Stream adapter: ReadAsync returns one WebSocket message + newline per call.
    /// StreamServerTransport reads line-by-line, so each read yields one JSON-RPC message.
    /// </summary>
    private sealed class WebSocketReadStream(ClientWebSocket ws, int maxSize) : Stream
    {
        private readonly byte[] _recvBuf = new byte[64 * 1024];
        private byte[]? _buffered;
        private int _offset;

        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_buffered is not null)
                return DrainBuffer(buffer.Span);

            while (true)
            {
                var payload = await ReadOneMessageAsync(ct).ConfigureAwait(false);
                if (payload is null)
                    return 0;

                var trimmed = TrimTrailing(payload);
                if (trimmed == 0) continue;

                var withNewline = new byte[trimmed + 1];
                payload.AsSpan(0, trimmed).CopyTo(withNewline);
                withNewline[trimmed] = (byte)'\n';

                if (withNewline.Length <= buffer.Length)
                {
                    withNewline.CopyTo(buffer);
                    return withNewline.Length;
                }

                _buffered = withNewline;
                _offset = 0;
                return DrainBuffer(buffer.Span);
            }
        }

        private int DrainBuffer(Span<byte> dest)
        {
            var remaining = _buffered!.Length - _offset;
            var toCopy = Math.Min(remaining, dest.Length);
            _buffered.AsSpan(_offset, toCopy).CopyTo(dest);
            _offset += toCopy;
            if (_offset >= _buffered.Length) _buffered = null;
            return toCopy;
        }

        private async Task<byte[]?> ReadOneMessageAsync(CancellationToken ct)
        {
            using var ms = new MemoryStream();

            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(_recvBuf, ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) return null;
                if (result.MessageType != WebSocketMessageType.Text)
                    throw new InvalidDataException($"Expected text frame, got {result.MessageType}");
                if (ms.Length + result.Count > maxSize)
                    throw new InvalidDataException($"Message exceeded {maxSize} bytes");
                ms.Write(_recvBuf, 0, result.Count);
            } while (!result.EndOfMessage);

            return ms.ToArray();
        }

        private static int TrimTrailing(byte[] data)
        {
            var len = data.Length;
            while (len > 0 && (data[len - 1] == '\n' || data[len - 1] == '\r')) len--;
            return len;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    }

    /// <summary>
    /// Stream adapter: buffers writes until newline, then sends one WebSocket text frame.
    /// StreamServerTransport writes NDJSON — each newline boundary = one JSON-RPC message.
    /// </summary>
    private sealed class WebSocketWriteStream(ClientWebSocket ws, ILogger log) : Stream
    {
        private readonly MemoryStream _buf = new();

        public override bool CanRead => false;
        public override bool CanWrite => true;
        public override bool CanSeek => false;

        public override void Write(byte[] buffer, int offset, int count)
            => WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            var data = buffer.ToArray();
            var start = 0;

            for (var i = 0; i < data.Length; i++)
            {
                if (data[i] != (byte)'\n') continue;

                _buf.Write(data, start, i - start);
                await FlushMessageAsync(ct).ConfigureAwait(false);
                start = i + 1;
            }

            if (start < data.Length)
                _buf.Write(data, start, data.Length - start);
        }

        public override async Task FlushAsync(CancellationToken ct)
        {
            if (_buf.Length > 0)
                await FlushMessageAsync(ct).ConfigureAwait(false);
        }

        private async Task FlushMessageAsync(CancellationToken ct)
        {
            if (_buf.Length == 0) return;

            var payload = _buf.ToArray();
            _buf.SetLength(0);

            var len = payload.Length;
            while (len > 0 && payload[len - 1] == '\r') len--;
            if (len == 0) return;

            log.ZLogDebug($"Tunnel send: {len} bytes");
            await ws.SendAsync(
                payload.AsMemory(0, len), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    }
}
