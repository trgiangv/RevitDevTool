using System.Diagnostics;

namespace DevTools.TestRunner.Core.Services;

/// <summary>
/// Visual Studio Stop Debugging terminates the MTP testhost (the process
/// passed as <c>--debug-parent-pid</c>), not the Autodesk host TestRunner
/// just spawned. Watch that testhost PID and cancel so launch wait can kill
/// the in-flight host.
/// </summary>
internal sealed class DebugHostLifetime : IAsyncDisposable
{
    private readonly CancellationTokenSource _linked;
    private readonly Task _watch;

    private DebugHostLifetime(CancellationTokenSource linked, Task watch)
    {
        _linked = linked;
        _watch = watch;
    }

    public CancellationToken Token => _linked.Token;

    public bool IsCancellationRequested => _linked.IsCancellationRequested;

    public static DebugHostLifetime Link(int? testhostProcessId, CancellationToken cancellationToken)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var watch = testhostProcessId is { } pid and > 0
            ? WatchAsync(pid, linked)
            : Task.CompletedTask;
        return new DebugHostLifetime(linked, watch);
    }

    public async ValueTask DisposeAsync()
    {
        TryCancel(_linked);
        _linked.Dispose();
        try
        {
            await _watch.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task WatchAsync(int processId, CancellationTokenSource linked)
    {
        try
        {
            Process process;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                TryCancel(linked);
                return;
            }

            if (process.HasExited)
            {
                TryCancel(linked);
                return;
            }

            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            TryCancel(linked);
        }
        catch (OperationCanceledException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void TryCancel(CancellationTokenSource linked)
    {
        try
        {
            if (!linked.IsCancellationRequested)
                linked.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // ignore
        }
    }
}
