using System.Diagnostics;

namespace DevTools.Hosting;

public enum HostStatus
{
    Ready,
    Exited,
    TimedOut,
    Cancelled
}

/// <summary>
/// Single wait loop for launched host processes. Ready probes stay at the caller.
/// </summary>
public static class HostLaunchWaiter
{
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

    public static async Task<HostStatus> UntilAsync(
        Process process,
        Func<bool> isReady,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        TimeSpan? pollInterval = null)
    {
        var poll = pollInterval ?? DefaultPollInterval;
        var clock = Stopwatch.StartNew();

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                return HostStatus.Cancelled;

            if (process.HasExited)
                return HostStatus.Exited;

            if (isReady())
                return HostStatus.Ready;

            var remaining = timeout - clock.Elapsed;
            if (remaining <= TimeSpan.Zero)
                return HostStatus.TimedOut;

            var delay = remaining < poll ? remaining : poll;
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return HostStatus.Cancelled;
            }
        }
    }

    /// <summary>
    /// The caller spawned this process. Cancel and timeout must not leave it booting.
    /// Ready keeps the process for reuse; Exited is already gone.
    /// </summary>
    public static void TerminateIfIncomplete(Process process, HostStatus status)
    {
        if (status is HostStatus.Ready or HostStatus.Exited)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }
}
