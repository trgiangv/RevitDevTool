using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.Hosting;

public sealed class HostMcpServerHostedService(
    HostMcpServerOptionsFactory optionsFactory,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IHostedService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<int, Task> _sessions = new();
    private readonly ILogger _logger = loggerFactory.CreateLogger<HostMcpServerHostedService>();
    private readonly string _pipeName = McpPipeName.Format(Environment.ProcessId);
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
                pipe = CurrentUserPipeFactory.CreateDuplexServer(_pipeName);
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
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Standard MCP pipe accept loop failed.");
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                pipe?.Dispose();
            }
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
        await using var transport = new StreamServerTransport(ownedPipe, ownedPipe, _pipeName, loggerFactory);
        await using var server = McpServer.Create(transport, optionsFactory.Create(), loggerFactory, serviceProvider);
        await server.RunAsync(cancellationToken).ConfigureAwait(false);
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
