using System.IO.Pipelines;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ZLogger;

namespace DevTools.McpServer;

/// <summary>
/// Outbound WebSocket tunnel connecting to a remote MCP gateway.
/// 
/// Gateway receives MCP Streamable HTTP from AI clients (ChatGPT, Perplexity, etc.)
/// and bridges JSON-RPC messages bidirectionally through this WebSocket tunnel.
/// MCPServer.exe initiates the connection outbound — no inbound ports, no admin rights.
///
/// Protocol over WebSocket:
///   - Each WebSocket text message = one JSON-RPC message (newline-delimited JSON)
///   - Gateway relays client JSON-RPC → WebSocket → this tunnel → McpServer
///   - McpServer responses flow back the same path
/// </summary>
public sealed class GatewayTunnelClient(
    Uri gatewayUri, 
    string? token, 
    McpServerOptions serverOptions, 
    ILoggerFactory loggerFactory, 
    ILogger logger) : IAsyncDisposable
{
    private ClientWebSocket? _ws;

    private const int ReconnectBaseDelayMs = 2_000;
    private const int ReconnectMaxDelayMs = 60_000;

    public async Task RunAsync(CancellationToken ct)
    {
        var delay = ReconnectBaseDelayMs;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAndServeAsync(ct).ConfigureAwait(false);
                delay = ReconnectBaseDelayMs;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.ZLogWarning(ex, $"Gateway connection lost, reconnecting in {delay}ms...");
            }

            try { await Task.Delay(delay, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }

            delay = Math.Min(delay * 2, ReconnectMaxDelayMs);
        }
    }

    private async Task ConnectAndServeAsync(CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ws = ws;

        if (!string.IsNullOrEmpty(token))
            ws.Options.SetRequestHeader("Authorization", $"Bearer {token}");

        logger.ZLogInformation($"Connecting to gateway {gatewayUri}...");
        await ws.ConnectAsync(gatewayUri, ct).ConfigureAwait(false);
        logger.ZLogInformation($"Connected to gateway");

        var pipe = new Pipe();
        var wsToPipe = PumpWebSocketToPipeAsync(ws, pipe.Writer, sessionCts.Token);
        var outputStream = new WebSocketOutputStream(ws, sessionCts.Token);

        await using var server = ModelContextProtocol.Server.McpServer.Create(
            new StreamServerTransport(pipe.Reader.AsStream(), outputStream, "GatewayTunnel", loggerFactory),
            serverOptions);

        var serverTask = server.RunAsync(sessionCts.Token);
        await Task.WhenAny(wsToPipe, serverTask).ConfigureAwait(false);
        await sessionCts.CancelAsync().ConfigureAwait(false);

        try { await wsToPipe.ConfigureAwait(false); } catch (OperationCanceledException) { }
        try { await serverTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Reads WebSocket messages and writes them into the pipe as newline-delimited JSON
    /// (the format StreamServerTransport expects: one JSON-RPC message per line).
    /// </summary>
    private static async Task PumpWebSocketToPipeAsync(ClientWebSocket ws, PipeWriter writer, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var payload = await ReadOneMessageAsync(ws, buffer, ct).ConfigureAwait(false);
                if (payload is null) return;
                if (payload.Length == 0) continue;

                var memory = writer.GetMemory(payload.Length + 1);
                payload.AsSpan().CopyTo(memory.Span);
                memory.Span[payload.Length] = (byte)'\n';
                writer.Advance(payload.Length + 1);

                var flushResult = await writer.FlushAsync(ct).ConfigureAwait(false);
                if (flushResult.IsCompleted) return;
            }
        }
        finally
        {
            await writer.CompleteAsync().ConfigureAwait(false);
        }
    }

    /// <returns>Message bytes, empty array for zero-length messages, or null on close frame.</returns>
    private static async Task<byte[]?> ReadOneMessageAsync(ClientWebSocket ws, byte[] buffer, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;

        do
        {
            result = await ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return ms.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        if (_ws is { State: WebSocketState.Open } ws)
        {
            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutting down", CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // best-effort
            }
        }

        _ws?.Dispose();
    }

    /// <summary>
    /// Wraps a WebSocket as a write-only Stream so StreamServerTransport can send
    /// JSON-RPC responses back through the tunnel.
    /// </summary>
    private sealed class WebSocketOutputStream(ClientWebSocket ws, CancellationToken ct) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
            => WriteAsync(buffer, offset, count, ct).GetAwaiter().GetResult();

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => await ws.SendAsync(
                new ArraySegment<byte>(buffer, offset, count),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken).ConfigureAwait(false);

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => await ws.SendAsync(
                buffer,
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken).ConfigureAwait(false);

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
