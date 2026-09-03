using System.IO.Pipes;
using System.Text;
using DevTools.Ipc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Daemon.Control;

internal sealed class ControlPipeHostedService(ControlPipeHandler handler, ILogger<ControlPipeHostedService> logger)
    : BackgroundService
{
    private const int MaxServerInstances = 4;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(
                IpcConstants.ControlPipeName,
                PipeDirection.InOut,
                MaxServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                await HandleConnectionAsync(server, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.ZLogWarning(ex, $"Control pipe transient error");
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        var buffer = new byte[4096];
        var bytesRead = await pipe.ReadAsync(buffer, ct).ConfigureAwait(false);
        if (bytesRead == 0) return;

        var line = Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimEnd('\r', '\n');

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var handlerTask = handler.HandleRequestAsync(line, linkedCts.Token);
        var monitorTask = WaitForDisconnectAsync(pipe, linkedCts.Token);

        var completed = await Task.WhenAny(handlerTask, monitorTask).ConfigureAwait(false);

        if (completed == monitorTask)
        {
            await linkedCts.CancelAsync().ConfigureAwait(false);
            logger.ZLogWarning($"Control pipe disconnected by client");
            return;
        }

        var response = await handlerTask.ConfigureAwait(false);

        if (!pipe.IsConnected) return;

        var responseBytes = Encoding.UTF8.GetBytes(response + "\n");
        await pipe.WriteAsync(responseBytes, ct).ConfigureAwait(false);
        await pipe.FlushAsync(ct).ConfigureAwait(false);
        pipe.WaitForPipeDrain();
    }

    private static async Task WaitForDisconnectAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        while (pipe.IsConnected && !ct.IsCancellationRequested)
            await Task.Delay(500, ct).ConfigureAwait(false);
    }
}
