using System.Text.Json;

namespace DevTools.Ipc;

/// <summary>
/// Bidirectional length-prefixed JSON framing over a raw stream.
/// Thread-safe writes via SemaphoreSlim. Single read loop dispatches to <see cref="MessageReceived"/>.
/// Message format: [4-byte little-endian body length][UTF-8 JSON body]
/// <see cref="MaxFrameBytes"/> stops a bad header from allocating without a limit.
/// </summary>
public sealed class BridgePipeConnection(Stream stream) : IDisposable
{
    public const int MaxFrameBytes = 16 * 1024 * 1024;

    private readonly Stream _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public event Action<BridgeMessage>? MessageReceived;
    public event Action<string>? FrameFault;
    public event Action? Disconnected;

    public Task ReadTask { get; private set; } = Task.CompletedTask;

    public Exception? ReadError { get; private set; }

    public void StartReadLoop() => ReadTask = ReadLoopAsync();

    public async Task WriteAsync(BridgeMessage message, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = JsonSerializer.SerializeToUtf8Bytes(message, IpcJsonContext.Default.BridgeMessage);
        if (body.Length > MaxFrameBytes)
            throw new InvalidOperationException(
                $"Test pipe frame is {body.Length} bytes. The limit is {MaxFrameBytes}.");

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
        try
        {
            await ReadMessagesAsync(_cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsStreamClosed(ex))
        {
        }
        catch (Exception ex)
        {
            ReadError = ex;
        }

        Disconnected?.Invoke();
    }

    private async Task ReadMessagesAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var read = await ReadMessageAsync(ct).ConfigureAwait(false);
            if (!Deliver(read))
                return;
        }
    }

    private bool Deliver(MessageRead read)
    {
        if (read.Fault is not null)
        {
            FrameFault?.Invoke(read.Fault);
            return false;
        }

        if (read.Message is null)
            return false;

        MessageReceived?.Invoke(read.Message);
        return true;
    }

    private static bool IsStreamClosed(Exception ex) =>
        ex is IOException or ObjectDisposedException or OperationCanceledException;

    private async Task<MessageRead> ReadMessageAsync(CancellationToken ct)
    {
        var header = new byte[4];
        if (!await ReadExactAsync(header, 0, 4, ct).ConfigureAwait(false))
            return MessageRead.End();

        var bodyLen = BitConverter.ToInt32(header, 0);
        if (bodyLen is <= 0 or > MaxFrameBytes)
            return MessageRead.Bad("Message length is invalid.");

        var body = new byte[bodyLen];
        if (!await ReadExactAsync(body, 0, bodyLen, ct).ConfigureAwait(false))
            return MessageRead.End();

        try
        {
            var message = JsonSerializer.Deserialize(body, IpcJsonContext.Default.BridgeMessage);
            return message is null
                ? MessageRead.Bad("Body is not a BridgeMessage.")
                : MessageRead.Ok(message);
        }
        catch (JsonException)
        {
            return MessageRead.Bad("Body is not a BridgeMessage.");
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

    private readonly struct MessageRead
    {
        public BridgeMessage? Message { get; private init; }
        public string? Fault { get; private init; }

        public static MessageRead Ok(BridgeMessage message) => new() { Message = message };
        public static MessageRead Bad(string fault) => new() { Fault = fault };
        public static MessageRead End() => new();
    }
}
