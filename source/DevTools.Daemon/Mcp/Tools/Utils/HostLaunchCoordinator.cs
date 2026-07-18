namespace DevTools.Daemon.Mcp.Tools.Utils;

/// <summary>Coordinates startup dialog handling after a host driver starts a process.</summary>
internal static class HostLaunchCoordinator
{
    /// <summary>
    /// Starts a PID-scoped dialog resolver that outlives the MCP request.
    /// Only the pre-start check uses <paramref name="cancellationToken"/>; once running,
    /// the resolver uses an independent 90s deadline so remaining add-in dialogs keep
    /// being dismissed after the tool returns (agents often cancel the request CT then).
    /// </summary>
    public static Task<StartupDialogResolverResult>? StartDialogResolver(
        int processId,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return null;
        return ResolveDialogsAsync(processId);
    }

    /// <summary>
    /// Best-effort snapshot of resolver progress. Returns null if still running after
    /// <paramref name="wait"/> — the background resolver continues independently.
    /// </summary>
    public static async Task<StartupDialogResolverResult?> TryAwaitResolverResultAsync(
        Task<StartupDialogResolverResult>? task,
        TimeSpan wait)
    {
        if (task is null) return null;
        if (task.IsCompletedSuccessfully) return task.Result;
        try
        {
            using var cts = new CancellationTokenSource(wait);
            return await task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch { return null; }
    }

    private static async Task<StartupDialogResolverResult> ResolveDialogsAsync(int processId)
    {
        // Independent of MCP request CT — must keep polling after tool response.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        try
        {
            return await StartupDialogResolver.RunAsync(
                processId,
                new StartupDialogResolverOptions(),
                TimeSpan.FromSeconds(90),
                cancellationToken: cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new StartupDialogResolverResult(TimedOut: false, Events: []);
        }
    }

}
