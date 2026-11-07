using DevTools.Update.Apply;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using ZLogger;

namespace DevTools.Update.Engine;

/// <summary>
///     Waits for the processes that lock a payload folder, once per process, and runs a retry when each one exits.
/// </summary>
internal sealed class LockerWatcher(Func<Task> retry, ILogger logger)
{
    private readonly HashSet<int> _watched = [];

    public void Watch(IEnumerable<LockingProcess> lockers, CancellationToken cancellationToken)
    {
        foreach (var locker in lockers)
        {
            lock (_watched)
            {
                if (!_watched.Add(locker.ProcessId)) continue;
            }

            Task.Run(() => RetryAfterExitAsync(locker.ProcessId, cancellationToken), cancellationToken)
                .ContinueWith(task => logger.ZLogWarning(task.Exception, $"A pending payload could not be applied"), TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    private async Task RetryAfterExitAsync(int processId, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
        }
        finally
        {
            lock (_watched) _watched.Remove(processId);
        }

        await retry().ConfigureAwait(false);
    }
}
