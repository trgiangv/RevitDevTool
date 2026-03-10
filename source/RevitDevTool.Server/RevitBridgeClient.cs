using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RevitDevTool.Contracts;

namespace RevitDevTool.Server;

public sealed class RevitBridgeClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    
    public bool IsConnected => _tcp?.Connected == true && _stream is not null;

    public async Task<bool> ConnectAsync(int port, CancellationToken cancellationToken = default)
    {
        try
        {
            _tcp = new TcpClient();
            await _tcp.ConnectAsync("127.0.0.1", port, cancellationToken).ConfigureAwait(false);
            _stream = _tcp.GetStream();

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Cleanup();
            return false;
        }
    }

    public async Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            return [];
        }

        var request = new Envelope
        {
            Kind = McpMessageKinds.Request,
            Action = McpActions.ListTools
        };

        var response = await SendAndReceiveAsync(request, cancellationToken).ConfigureAwait(false);
        if (response is null || response.Error is not null)
            return [];

        var toolsWrapper = JsonSerializer.Deserialize<ToolsListPayload>(response.PayloadJson, JsonOptions);
        return toolsWrapper?.Tools ?? [];
    }

    public async Task<McpToolExecutionResult> CallToolAsync(
        string toolId,
        string toolName,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            return McpToolExecutionResult.Failed("bridge.disconnected", "Revit bridge is not connected.");
        }

        var request = new Envelope
        {
            Kind = McpMessageKinds.Request,
            Action = McpActions.ToolCall,
            ToolId = toolId,
            ToolName = toolName,
            PayloadJson = payloadJson
        };

        var response = await SendAndReceiveAsync(request, cancellationToken).ConfigureAwait(false);
        if (response is null)
            return McpToolExecutionResult.Failed("bridge.disconnected", "Revit bridge connection lost during tool call.");

        if (response.Error is not null)
        {
            return McpToolExecutionResult.Failed(
                response.Error.Code,
                response.Error.Message,
                response.Error.Details);
        }

        return McpToolExecutionResult.Succeeded(
            response.PayloadJson,
            response.Message ?? string.Empty,
            response.ResultKind ?? McpResultKinds.Json);
    }

    private async Task<Envelope?> SendAndReceiveAsync(Envelope request, CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            return null;
        }

        try
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await WriteEnvelopeAsync(_stream, request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }

            while (true)
            {
                var response = await ReadEnvelopeAsync(_stream, cancellationToken).ConfigureAwait(false);
                if (response is null)
                {
                    Cleanup();
                    return null;
                }

                if (response.Kind == McpMessageKinds.Event)
                    continue;

                return response;
            }
        }
        catch
        {
            Cleanup();
            return null;
        }
    }

    private void Cleanup()
    {
        _stream = null;
        try { _tcp?.Close(); } catch { /* ignored */ }
        _tcp = null;
    }

    private static async Task WriteEnvelopeAsync(NetworkStream stream, Envelope envelope, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        var payload = Encoding.UTF8.GetBytes(json);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Envelope?> ReadEnvelopeAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = await ReadExactAsync(stream, 4, cancellationToken).ConfigureAwait(false);
        if (header is null)
            return null;

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (payloadLength is <= 0 or > 4 * 1024 * 1024)
            throw new InvalidDataException($"Invalid payload length: {payloadLength}");

        var payloadBytes = await ReadExactAsync(stream, payloadLength, cancellationToken).ConfigureAwait(false);
        if (payloadBytes is null)
            return null;

        var payloadJson = Encoding.UTF8.GetString(payloadBytes);
        return JsonSerializer.Deserialize<Envelope>(payloadJson, JsonOptions);
    }

    private static async Task<byte[]?> ReadExactAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return offset == 0 ? null : throw new EndOfStreamException("Socket closed mid-frame.");
            offset += read;
        }

        return buffer;
    }

    public ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _tcp?.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed record ToolsListPayload
    {
        public List<McpToolDefinition> Tools { get; init; } = [];
    }
}
