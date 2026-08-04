using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.Logging;

namespace DevTools.Mcp.Adapter.Host;

/// <summary>
/// Newline-delimited JSON-RPC session over a connected named pipe (same framing as SDK stream transport).
/// </summary>
public sealed class McpPipeSession : IAsyncDisposable
{
    private static readonly byte[] Newline = "\n"u8.ToArray();

    private readonly Stream _stream;
    private readonly IMcpHandler _handler;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _disposed;

    private McpPipeSession(
        Stream stream,
        IMcpHandler handler,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        _stream = stream;
        _handler = handler;
        _logger = logger;
        Completion = Task.Run(() => ReadLoopAsync(cancellationToken), cancellationToken);
    }

    public static McpPipeSession Start(
        Stream stream,
        IMcpHandler handler,
        CancellationToken cancellationToken) =>
        Start(stream, handler, null, cancellationToken);

    public static McpPipeSession Start(
        Stream stream,
        IMcpHandler handler,
        ILogger? logger,
        CancellationToken cancellationToken) =>
        new(stream, handler, logger, cancellationToken);

    public Task Completion { get; }

    public Task SendNotificationAsync(string method, CancellationToken cancellationToken = default) =>
        SendAsync(McpJsonRpc.CreateNotification(method), cancellationToken);

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        using var reader = CreateReader();
        while (!cancellationToken.IsCancellationRequested &&
               await ProcessNextLineAsync(reader, cancellationToken).ConfigureAwait(false))
        {
        }
    }

    private StreamReader CreateReader() =>
        new(
            _stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
#if NETFRAMEWORK
            bufferSize: 1024,
#endif
            leaveOpen: true);

    private async Task<bool> ProcessNextLineAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var line = await TryReadLineAsync(reader, cancellationToken).ConfigureAwait(false);
        if (line is null)
            return false;

        if (string.IsNullOrWhiteSpace(line))
            return true;

        var request = TryParseRequest(line);
        if (request is null)
            return true;

        return await DispatchRequestAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> TryReadLineAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private JsonObject? TryParseRequest(string line)
    {
        try
        {
            return JsonNode.Parse(line)?.AsObject();
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "MCP: invalid JSON-RPC line");
            return null;
        }
    }

    private async Task<bool> DispatchRequestAsync(JsonObject request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            if (response is not null)
                await SendAsync(response, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "MCP handler error");
            return true;
        }
    }

    private async Task SendAsync(JsonObject response, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(response);
            await _stream.WriteAsync(json, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(Newline, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            await Completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignored
        }

        _sendLock.Dispose();
        _stream.Dispose();
    }
}
