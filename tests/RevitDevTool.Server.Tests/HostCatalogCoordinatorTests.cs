using DevTools.Daemon.Mcp;

namespace RevitDevTool.Server.Tests;

public sealed class HostCatalogCoordinatorTests
{
    [Fact]
    public async Task RequestRefresh_CoalescesConcurrentRequestsIntoSerializedRebuilds()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var rebuilds = 0;
        var coordinator = new HostCatalogCoordinator(async _ =>
        {
            Interlocked.Increment(ref rebuilds);
            entered.TrySetResult(true);
            await release.Task.ConfigureAwait(false);
        });

        coordinator.RequestRefresh();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        coordinator.RequestRefresh();
        coordinator.RequestRefresh();
        release.TrySetResult(true);

        await coordinator.WaitForIdleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, rebuilds);
    }

    [Fact]
    public async Task DisposeAsync_CancelsAndAwaitsBlockedRefresh()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new HostCatalogCoordinator(async ct =>
        {
            entered.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                cancelled.TrySetResult(true);
                throw;
            }
        });

        coordinator.RequestRefresh();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        await coordinator.DisposeAsync();

        await cancelled.Task.WaitAsync(TestContext.Current.CancellationToken);
    }
}
