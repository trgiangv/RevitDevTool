using System.Diagnostics;

namespace DevTools.Hosting;

/// <summary>
/// Single wait loop for launched host processes. Ready probes stay at the caller.
/// </summary>
public static class HostLaunchWait
{
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

    public static async Task<HostReadyStatus> UntilAsync(
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
                return HostReadyStatus.Cancelled;

            if (process.HasExited)
                return HostReadyStatus.Exited;

            if (isReady())
                return HostReadyStatus.Ready;

            var remaining = timeout - clock.Elapsed;
            if (remaining <= TimeSpan.Zero)
                return HostReadyStatus.TimedOut;

            var delay = remaining < poll ? remaining : poll;
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return HostReadyStatus.Cancelled;
            }
        }
    }
}
