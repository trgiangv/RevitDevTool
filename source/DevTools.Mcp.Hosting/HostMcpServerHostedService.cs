using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Hosting;

public sealed class HostMcpServerHostedService(
    HostMcpServerOptionsFactory optionsFactory,
    IHostAppInfo hostInfo,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider,
    Func<string, NamedPipeServerStream>? createServerPipe = null) : IHostedService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<int, Task> _sessions = new();
    private readonly Func<string, NamedPipeServerStream> _createServerPipe =
        createServerPipe ?? (pipeName => CurrentUserPipeFactory.CreateDuplexServer(pipeName));
    private readonly ILogger _logger = loggerFactory.CreateLogger<HostMcpServerHostedService>();
    private readonly string _pipeName = HostPipeName.Format(
        hostInfo.Host.ToString(),
        hostInfo.VersionNumber,
        Environment.ProcessId);
    private CancellationTokenSource? _stoppingSource;
    private Task? _acceptLoopTask;
    private int _nextSessionId;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_stoppingSource is not null)
            return Task.CompletedTask;

        _stoppingSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoopTask = AcceptLoopAsync(_stoppingSource.Token);
        _logger.LogInformation("Listening for standard MCP sessions on pipe '{PipeName}'.", _pipeName);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var stoppingSource = _stoppingSource;
        if (stoppingSource is null)
            return;

        stoppingSource.Cancel();
        try
        {
            if (_acceptLoopTask is not null)
                await _acceptLoopTask.ConfigureAwait(false);

            var sessions = _sessions.Values.ToArray();
            if (sessions.Length > 0)
            {
                try
                {
                    await Task.WhenAll(sessions).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Individual session failures are logged by ObserveSessionAsync.
                }
            }
        }
        finally
        {
            _sessions.Clear();
            _acceptLoopTask = null;
            _stoppingSource = null;
            stoppingSource.Dispose();
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = _createServerPipe(_pipeName);
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                var sessionId = Interlocked.Increment(ref _nextSessionId);
                var session = RunSessionAsync(pipe, cancellationToken);
                _sessions[sessionId] = session;
                _ = ObserveSessionAsync(sessionId, session);
                pipe = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException exception) when (IsPipeInstancesBusy(exception))
            {
                await DelayBeforeRetryAsync(200, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Standard MCP pipe accept loop failed.");
                await DelayBeforeRetryAsync(500, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                pipe?.Dispose();
            }
        }
    }

    private static async Task DelayBeforeRetryAsync(int millisecondsDelay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(millisecondsDelay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host shutdown ends retry delays and the enclosing accept loop.
        }
    }

    private async Task ObserveSessionAsync(int sessionId, Task session)
    {
        try
        {
            await session.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stoppingSource?.IsCancellationRequested == true)
        {
            // Host shutdown ends active sessions.
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Standard MCP session ended with an error.");
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
        }
    }

    private async Task RunSessionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using var ownedPipe = pipe;
        var input = await CreateMcpInputStreamAsync(ownedPipe, cancellationToken).ConfigureAwait(false);
        if (input is null)
            return;

        await using var transport = new StreamServerTransport(input, ownedPipe, _pipeName, loggerFactory);
        await using var server = McpServer.Create(transport, optionsFactory.Create(), loggerFactory, serviceProvider);
        await server.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Stream?> CreateMcpInputStreamAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        var prefix = new byte[4];
        var offset = 0;
        while (offset < prefix.Length)
        {
            var read = await pipe.ReadAsync(prefix, offset, prefix.Length - offset, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return null;
            offset += read;
        }

        if (prefix.Contains((byte)0))
        {
            _logger.LogWarning(
                "Rejected a non-MCP frame on pipe '{PipeName}'. Clients must speak standard MCP NDJSON; legacy framed bridges are not supported.",
                _pipeName);
            return null;
        }

        return new PrefixReadStream(pipe, prefix);
    }

    private sealed class PrefixReadStream(Stream inner, byte[] prefix) : Stream
    {
        private int _offset;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_offset >= prefix.Length)
                return inner.Read(buffer, offset, count);

            var copied = Math.Min(count, prefix.Length - _offset);
            Buffer.BlockCopy(prefix, _offset, buffer, offset, copied);
            _offset += copied;
            return copied;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _offset >= prefix.Length
                ? inner.ReadAsync(buffer, offset, count, cancellationToken)
                : Task.FromResult(Read(buffer, offset, count));

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            inner.WriteAsync(buffer, offset, count, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            // The owning session disposes the named pipe after the MCP transport has completed.
        }
    }

    private static bool IsPipeInstancesBusy(IOException exception)
    {
        const int AllPipeInstancesBusy = 231;
        var win32Code = exception.HResult & 0xFFFF;
        return win32Code == AllPipeInstancesBusy ||
               exception.Message.Contains("All pipe instances are busy", StringComparison.OrdinalIgnoreCase);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
