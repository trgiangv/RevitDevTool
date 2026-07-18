using DevTools.Daemon.Mcp.Tools;
using DevTools.Mcp.Routing.Catalog;
using Microsoft.Extensions.Logging;

namespace DevTools.Daemon.Mcp;

public sealed class HostCatalogCoordinator : IAsyncDisposable
{
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly Lock lifecycleGate = new();
    private readonly Func<CancellationToken, Task> rebuildSnapshotAsync;
    private readonly ILogger<HostCatalogCoordinator>? logger;
    private int refreshRequested;
    private Task? refreshTask;
    private Task? disposeTask;
    private TaskCompletionSource<bool>? refreshesDrained;
    private int activeRefreshes;
    private bool disposing;

    public HostCatalogCoordinator(McpEngine engine, ILogger<CatalogService> catalogLogger, ILogger<HostCatalogCoordinator> logger)
    {
        var catalogService = new CatalogService(
            engine.InstanceManager,
            engine.ToolCollection,
            engine.PromptCollection,
            engine.ResourceCollection,
            engine.DynamicToolCatalog,
            engine.DynamicResourceCatalog,
            engine.DynamicPromptCatalog,
            engine.LocalTools,
            catalogLogger,
            CancellationToken.None);
        rebuildSnapshotAsync = catalogService.RebuildCatalogAsync;
        this.logger = logger;

        var refreshTool = engine.LocalTools.OfType<RefreshDynamicCatalog>().FirstOrDefault();
        if (refreshTool is not null)
            refreshTool.RefreshDelegate = RebuildSnapshotAsync;
    }

    internal HostCatalogCoordinator(Func<CancellationToken, Task> rebuildSnapshotAsync)
    {
        this.rebuildSnapshotAsync = rebuildSnapshotAsync;
    }

    public void RequestRefresh()
    {
        lock (lifecycleGate)
        {
            if (disposing)
                return;

            if (Interlocked.Exchange(ref refreshRequested, 1) == 0)
            {
                BeginRefreshLocked();
                refreshTask = RefreshLoopAsync(lifetimeCancellation.Token);
            }
        }
    }

    public Task RebuildSnapshotAsync(CancellationToken ct = default)
    {
        lock (lifecycleGate)
        {
            if (disposing)
                return Task.FromCanceled(new CancellationToken(canceled: true));

            BeginRefreshLocked();
        }

        return RebuildSnapshotCoreAsync(ct);
    }

    private async Task RebuildSnapshotCoreAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetimeCancellation.Token);
        var gateHeld = false;
        try
        {
            await refreshGate.WaitAsync(linked.Token).ConfigureAwait(false);
            gateHeld = true;
            await rebuildSnapshotAsync(linked.Token).ConfigureAwait(false);
        }
        finally
        {
            if (gateHeld)
                refreshGate.Release();
            CompleteRefresh();
        }
    }

    internal async Task WaitForIdleAsync(CancellationToken ct)
    {
        while (true)
        {
            await refreshGate.WaitAsync(ct).ConfigureAwait(false);
            refreshGate.Release();
            if (Volatile.Read(ref refreshRequested) == 0)
                return;
        }
    }

    private async Task RefreshLoopAsync(CancellationToken ct)
    {
        var gateHeld = false;
        try
        {
            await refreshGate.WaitAsync(ct).ConfigureAwait(false);
            gateHeld = true;
            while (Interlocked.Exchange(ref refreshRequested, 0) != 0)
            {
                try
                {
                    await rebuildSnapshotAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Catalog refresh failed.");
                }
            }
        }
        finally
        {
            if (gateHeld)
                refreshGate.Release();
            CompleteRefresh();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (lifecycleGate)
        {
            if (disposeTask is not null)
                return new ValueTask(disposeTask);

            disposing = true;
            lifetimeCancellation.Cancel();
            disposeTask = DisposeCoreAsync(GetRefreshesDrainedTaskLocked());
            return new ValueTask(disposeTask);
        }
    }

    private async Task DisposeCoreAsync(Task refreshesDrainedTask)
    {
        await refreshesDrainedTask.ConfigureAwait(false);

        refreshGate.Dispose();
        lifetimeCancellation.Dispose();
    }

    private void BeginRefreshLocked() => activeRefreshes++;

    private void CompleteRefresh()
    {
        lock (lifecycleGate)
        {
            activeRefreshes--;
            if (activeRefreshes == 0)
                refreshesDrained?.TrySetResult(true);
        }
    }

    private Task GetRefreshesDrainedTaskLocked()
    {
        if (activeRefreshes == 0)
            return Task.CompletedTask;

        return (refreshesDrained ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
    }
}
