using DevTools.Utilities;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace DevTools.Presentation.Services;

public static class DaemonClient
{
    private const int ConnectTimeoutMs = 2000;
    private const int ReadyPollIntervalMs = 500;
    private const int ReadyPollMaxAttempts = 20;
    private const int QuickProbeTimeoutMs = 300;

    /// <summary>
    /// Ensures the Daemon process is running and its daemon control pipe is ready.
    /// Launches it if not already running, then polls until the pipe accepts connections.
    /// </summary>
    public static async Task EnsureRunningAsync(CancellationToken ct = default)
    {
        if (await IsReadyAsync(ct).ConfigureAwait(false)) return;

        var exePath = Utilities.AppUtils.GetDaemonExePath();
        if (!System.IO.File.Exists(exePath)) return;

        Process.Start(new ProcessStartInfo(exePath, IpcConstants.DaemonArgs.Background) { UseShellExecute = true });

        for (var i = 0; i < ReadyPollMaxAttempts; i++)
        {
            await Task.Delay(ReadyPollIntervalMs, ct).ConfigureAwait(false);
            if (await IsReadyAsync(ct).ConfigureAwait(false)) return;
        }
    }

    public static async Task SendAsync(string method, CancellationToken ct = default)
    {
        try
        {
#if NET
            await using var pipe = new NamedPipeClientStream(
#else
            using var pipe = new NamedPipeClientStream(
#endif
                ".", IpcConstants.ControlPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(ConnectTimeoutMs, ct).ConfigureAwait(false);

            var request = Encoding.UTF8.GetBytes($"{{\"method\":\"{method}\"}}\n");
#if NET
            await pipe.WriteAsync(request, ct).ConfigureAwait(false);
#else
            await pipe.WriteAsync(request, 0, request.Length, ct).ConfigureAwait(false);
#endif
            await pipe.FlushAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // The daemon control pipe is not listening.
        }
    }

    private static async Task<bool> IsReadyAsync(CancellationToken ct = default)
    {
        try
        {
#if NET
            await using var pipe = new NamedPipeClientStream(
#else
            using var pipe = new NamedPipeClientStream(
#endif
                ".", IpcConstants.ControlPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(QuickProbeTimeoutMs, ct).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
