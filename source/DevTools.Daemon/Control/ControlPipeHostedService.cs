using System.IO;
using System.IO.Pipes;
using System.Text;
using DevTools.Ipc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Daemon.Control;

internal sealed class ControlPipeHostedService(
    ControlPipeHandler handler,
    ILogger<ControlPipeHostedService> logger) : BackgroundService
{
    private const int MaxCommandBytes = 1024;

    private Task _connection = Task.CompletedTask;

    internal string PipeName { get; init; } = IpcConstants.ControlPipeName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = NamedPipes.Open(PipeName);
                await server.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                var connected = server;
                server = null;
                _connection = HandleConnectionAsync(connected, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                server?.Dispose();
                break;
            }
            catch (IOException ex) when (NamedPipes.IsBusy(ex))
            {
                server?.Dispose();
                await Task.Delay(200, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                server?.Dispose();
                logger.ZLogWarning(ex, $"Daemon control pipe accept failed");
                await Task.Delay(500, stoppingToken).ConfigureAwait(false);
            }
        }

        await _connection.ConfigureAwait(false);
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        try
        {
            var line = await ReadCommandAsync(server, ct).ConfigureAwait(false);
            if (line is not null)
                await handler.HandleRequestAsync(line, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Daemon control pipe command failed");
        }
        finally
        {
            await server.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<string?> ReadCommandAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        var buffer = new byte[MaxCommandBytes];
        var count = 0;
        while (count < MaxCommandBytes)
        {
            var read = await pipe.ReadAsync(buffer.AsMemory(count, MaxCommandBytes - count), ct).ConfigureAwait(false);
            if (read == 0)
                break;

            count += read;
            var newline = Array.IndexOf(buffer, (byte)'\n', 0, count);
            if (newline > 0)
                return Encoding.UTF8.GetString(buffer, 0, newline).TrimEnd('\r');
            if (newline == 0)
                return null;
        }

        return null;
    }
}
