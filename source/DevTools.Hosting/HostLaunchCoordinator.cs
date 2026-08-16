namespace DevTools.Hosting;

/// <summary>Startup dialog auto-dismiss for launched host processes.</summary>
public static class HostLaunchCoordinator
{
    public static StartupDialogResolverHandle? StartDialogResolver(
        IHostStartupDialogStrategy? strategy,
        int processId,
        CancellationToken cancellationToken)
    {
        if (strategy is null)
            return null;
        if (cancellationToken.IsCancellationRequested)
            return null;

        return StartupDialogResolverHandle.Start(processId, strategy.CreateOptions(), cancellationToken);
    }

    /// <summary>
    /// Best-effort snapshot of resolver progress. Returns null if still running after
    /// <paramref name="wait"/> — the background resolver continues independently until disposed.
    /// </summary>
    public static async Task<StartupDialogResolverResult?> TryAwaitResolverResultAsync(
        StartupDialogResolverHandle? handle,
        TimeSpan wait)
    {
        if (handle is null)
            return null;

        var task = handle.Completion;
        if (task.Status == TaskStatus.RanToCompletion)
            return task.Result;

        try
        {
            using var cts = new CancellationTokenSource(wait);
            var delay = Task.Delay(wait, cts.Token);
            var completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
            if (completed != task)
                return null;

            return await task.ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}
