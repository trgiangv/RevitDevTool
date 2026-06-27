using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevTools.Ipc;

/// <summary>
/// Bidirectional length-prefixed JSON framing over a raw stream.
/// Thread-safe writes via SemaphoreSlim. Single read loop dispatches to <see cref="MessageReceived"/>.
/// Protocol: [4-byte little-endian body length][UTF-8 JSON body]
/// MaxMessageSize prevents a malformed header from allocating unbounded memory.
/// </summary>
public sealed class BridgePipeConnection(Stream stream) : IDisposable
{
    private const int MaxMessageSize = 16 * 1024 * 1024;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Stream _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public event Action<BridgeMessage>? MessageReceived;
    public event Action? Disconnected;

    public void StartReadLoop() => _ = ReadLoopAsync();

    public async Task WriteAsync(BridgeMessage message, CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BridgePipeConnection));
        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        var header = BitConverter.GetBytes(body.Length);

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(header.AsMemory(0, 4), ct).ConfigureAwait(false);
            await _stream.WriteAsync(body.AsMemory(), ct).ConfigureAwait(false);
            await _stream.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync()
    {
        var ct = _cts.Token;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var msg = await ReadFrameAsync(ct).ConfigureAwait(false);
                if (msg is null) break;
                MessageReceived?.Invoke(msg);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // ignore
        }

        Disconnected?.Invoke();
    }

    private async Task<BridgeMessage?> ReadFrameAsync(CancellationToken ct)
    {
        var header = new byte[4];
        if (!await ReadExactAsync(header, 0, 4, ct).ConfigureAwait(false))
            return null;

        var bodyLen = BitConverter.ToInt32(header, 0);
        if (bodyLen is <= 0 or > MaxMessageSize)
            return null;

        var body = new byte[bodyLen];
        if (!await ReadExactAsync(body, 0, bodyLen, ct).ConfigureAwait(false))
            return null;

        try
        {
            return JsonSerializer.Deserialize<BridgeMessage>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<bool> ReadExactAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        while (count > 0)
        {
            var read = await _stream.ReadAsync(buffer.AsMemory(offset, count), ct).ConfigureAwait(false);
            if (read == 0) return false;
            offset += read;
            count -= read;
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _writeLock.Dispose();
        _stream.Dispose();
    }
}
