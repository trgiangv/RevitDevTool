using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Hosting;

/// <summary>Owns one SDK server and stream pair for one gateway logical session.</summary>
public sealed class GatewayMcpSession : IAsyncDisposable
{
    private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource lifetime;
    private readonly ChannelReadStream input;
    private readonly GatewayWriteStream output;
    private readonly McpServer server;
    private readonly Task runTask;
    private int disposed;

    public GatewayMcpSession(
        string sessionId,
        Func<McpServerOptions> optionsFactory,
        Func<GatewayTunnelEnvelope, CancellationToken, ValueTask> sendAsync,
        ILoggerFactory loggerFactory,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        SessionId = sessionId;
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        input = new ChannelReadStream(inbound.Reader, lifetime.Token);
        output = new GatewayWriteStream(sessionId, sendAsync, lifetime.Token);
        server = McpServer.Create(
            new StreamServerTransport(input, output, $"Gateway:{sessionId}", loggerFactory),
            optionsFactory(), loggerFactory, services);
        runTask = RunAsync();
    }

    public string SessionId { get; }

    public ValueTask SendAsync(GatewayTunnelEnvelope envelope, CancellationToken cancellationToken) =>
        output.SendAsync(envelope, cancellationToken);

    public ValueTask<bool> RouteAsync(JsonElement message, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref disposed) != 0) return ValueTask.FromResult(false);
        var payload = Encoding.UTF8.GetBytes(message.GetRawText() + "\n");
        return ValueTask.FromResult(inbound.Writer.TryWrite(payload));
    }

    private async Task RunAsync()
    {
        try
        {
            await server.RunAsync(lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        finally
        {
            inbound.Writer.TryComplete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await lifetime.CancelAsync().ConfigureAwait(false);
        inbound.Writer.TryComplete();
        try { await runTask.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        await server.DisposeAsync().ConfigureAwait(false);
        await output.DisposeAsync().ConfigureAwait(false);
        await input.DisposeAsync().ConfigureAwait(false);
        lifetime.Dispose();
    }

    private sealed class ChannelReadStream(ChannelReader<byte[]> reader, CancellationToken lifetime) : Stream
    {
        private byte[]? current;
        private int offset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (current is null || offset == current.Length)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime, cancellationToken);
                try { current = await reader.ReadAsync(linked.Token).ConfigureAwait(false); }
                catch (ChannelClosedException) { return 0; }
                offset = 0;
            }

            var count = Math.Min(buffer.Length, current.Length - offset);
            current.AsMemory(offset, count).CopyTo(buffer);
            offset += count;
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class GatewayWriteStream(
        string sessionId,
        Func<GatewayTunnelEnvelope, CancellationToken, ValueTask> sendAsync,
        CancellationToken lifetime) : Stream
    {
        private readonly MemoryStream buffer = new();
        private readonly SemaphoreSlim writeLock = new(1, 1);

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Write(byte[] source, int offset, int count) =>
            WriteAsync(source.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (var value in source.ToArray())
                {
                    if (value == (byte)'\n') await FlushMessageAsync(cancellationToken).ConfigureAwait(false);
                    else buffer.WriteByte(value);
                }
            }
            finally { writeLock.Release(); }
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { await FlushMessageAsync(cancellationToken).ConfigureAwait(false); }
            finally { writeLock.Release(); }
        }

        public override void Flush() => FlushAsync(CancellationToken.None).GetAwaiter().GetResult();

        private async ValueTask FlushMessageAsync(CancellationToken cancellationToken)
        {
            if (buffer.Length == 0) return;
            using var document = JsonDocument.Parse(buffer.ToArray());
            buffer.SetLength(0);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime, cancellationToken);
            await sendAsync(new GatewayTunnelEnvelope(GatewayTunnelEnvelope.ProtocolVersion, GatewayTunnelEnvelope.McpMessage, sessionId, document.RootElement.Clone()), linked.Token)
                .ConfigureAwait(false);
        }

        public ValueTask SendAsync(GatewayTunnelEnvelope envelope, CancellationToken cancellationToken) =>
            sendAsync(envelope, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing) { buffer.Dispose(); writeLock.Dispose(); }
            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
