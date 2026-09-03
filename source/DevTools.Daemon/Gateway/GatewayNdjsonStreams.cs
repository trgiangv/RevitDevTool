using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Daemon.Gateway;

internal sealed class WebSocketReadStream(ClientWebSocket ws, int maxSize) : Stream
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

internal sealed class WebSocketWriteStream(ClientWebSocket ws, ILogger log) : Stream
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
