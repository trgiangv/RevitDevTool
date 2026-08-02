using System.IO.Pipes;
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
    private readonly Task _readLoop;
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
        _readLoop = Task.Run(() => ReadLoopAsync(cancellationToken), cancellationToken);
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

    public static async Task RunAsync(
        NamedPipeServerStream pipe,
        IMcpHandler handler,
        ILogger? logger,
        CancellationToken cancellationToken = default)
    {
        await using var session = Start(pipe, handler, logger, cancellationToken);
    }

    public Task Completion => _readLoop;

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            _stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
#if NETFRAMEWORK
            bufferSize: 1024,
#endif
            leaveOpen: true);
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (line is null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            JsonObject? request;
            try
            {
                request = JsonNode.Parse(line)?.AsObject();
                if (request is null)
                    continue;
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning(ex, "MCP: invalid JSON-RPC line");
                continue;
            }

            JsonObject? response;
            try
            {
                response = await _handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "MCP handler error");
                continue;
            }

            if (response is not null)
                await SendAsync(response, cancellationToken).ConfigureAwait(false);
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
            await _readLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignored
        }

        _sendLock.Dispose();

        if (_stream is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else
            _stream.Dispose();
    }
}
