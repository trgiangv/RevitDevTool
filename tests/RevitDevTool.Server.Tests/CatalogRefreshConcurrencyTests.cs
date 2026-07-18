using DevTools.Daemon.Mcp;

namespace RevitDevTool.Server.Tests;

public sealed class CatalogRefreshConcurrencyTests
{
    [Fact]
    public async Task OneHundredNotifications_CoalesceIntoAtMostTwoSerializedRebuilds()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maxConcurrent = 0;
        var total = 0;
        await using var coordinator = new HostCatalogCoordinator(async _ =>
        {
            var current = Interlocked.Increment(ref active);
            maxConcurrent = Math.Max(maxConcurrent, current);
            Interlocked.Increment(ref total);
            entered.TrySetResult(true);
            await release.Task.ConfigureAwait(false);
            Interlocked.Decrement(ref active);
        });

        coordinator.RequestRefresh();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(Enumerable.Range(0, 99).Select(_ => Task.Run(coordinator.RequestRefresh)));
        release.TrySetResult(true);
        await coordinator.WaitForIdleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, maxConcurrent);
        Assert.InRange(total, 1, 2);
    }
}
