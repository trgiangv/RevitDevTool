using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;

namespace RevitDevTool.Contracts;

public static class BridgeFrameCodec
{
    private const int MaxPayloadSize = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;

    public static async Task<Envelope?> ReadEnvelopeAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = await ReadExactAsync(stream, 4, cancellationToken).ConfigureAwait(false);
        if (header is null)
            return null;

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (payloadLength is <= 0 or > MaxPayloadSize)
            throw new InvalidDataException($"Invalid payload length: {payloadLength}");

        var payloadBytes = await ReadExactAsync(stream, payloadLength, cancellationToken).ConfigureAwait(false);
        if (payloadBytes is null)
            return null;

        var payloadJson = Encoding.UTF8.GetString(payloadBytes);
        return JsonSerializer.Deserialize<Envelope>(payloadJson, JsonOptions);
    }

    public static async Task WriteEnvelopeAsync(NetworkStream stream, Envelope envelope, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        var payload = Encoding.UTF8.GetBytes(json);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
#if NET
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
#else
        await stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
#endif
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static JsonElement SerializeBody<TBody>(TBody body)
    {
        return JsonSerializer.SerializeToElement(body, JsonOptions);
    }

    public static TBody? ReadBody<TBody>(Envelope envelope)
    {
        if (envelope.Body is not { } body || body.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return default;

        return JsonSerializer.Deserialize<TBody>(body.GetRawText(), JsonOptions);
    }

    private static async Task<byte[]?> ReadExactAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
#if NET
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken).ConfigureAwait(false);
#else
            var read = await stream.ReadAsync(buffer, offset, length - offset, cancellationToken).ConfigureAwait(false);
#endif
            if (read == 0)
                return offset == 0 ? null : throw new EndOfStreamException("Socket closed mid-frame.");
            offset += read;
        }

        return buffer;
    }
}
