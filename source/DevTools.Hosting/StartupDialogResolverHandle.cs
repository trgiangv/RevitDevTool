namespace DevTools.Hosting;

/// <summary>Owns the dialog-resolver task. Caller cancellation / dispose is the stop valve.</summary>
public sealed class StartupDialogResolverHandle : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private bool _disposed;

    private StartupDialogResolverHandle(CancellationTokenSource cts, Task<StartupDialogResolverResult> completion)
    {
        _cts = cts;
        Completion = completion;
    }

    public Task<StartupDialogResolverResult> Completion { get; }

    public static StartupDialogResolverHandle Start(
        int processId,
        StartupDialogResolverOptions options,
        CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var task = StartupDialogResolver.RunAsync(processId, options, cts.Token);
        return new StartupDialogResolverHandle(cts, task);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // already disposed
        }

        _cts.Dispose();
    }
}
