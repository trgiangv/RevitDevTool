using DevTools.Daemon.Mcp.Tools;
using DevTools.Mcp.Routing.Catalog;
using Microsoft.Extensions.Logging;

namespace DevTools.Daemon.Mcp;

public sealed class HostCatalogCoordinator : IAsyncDisposable
{
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly Lock refreshTaskGate = new();
    private readonly Func<CancellationToken, Task> rebuildSnapshotAsync;
    private readonly ILogger<HostCatalogCoordinator>? logger;
    private int refreshRequested;
    private Task? refreshTask;

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
        lock (refreshTaskGate)
        {
            if (lifetimeCancellation.IsCancellationRequested)
                return;

            if (Interlocked.Exchange(ref refreshRequested, 1) == 0)
                refreshTask = RefreshLoopAsync(lifetimeCancellation.Token);
        }
    }

    public async Task RebuildSnapshotAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetimeCancellation.Token);
        await refreshGate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            await rebuildSnapshotAsync(linked.Token).ConfigureAwait(false);
        }
        finally
        {
            refreshGate.Release();
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
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? task;
        lock (refreshTaskGate)
        {
            lifetimeCancellation.Cancel();
            task = refreshTask;
        }

        if (task is not null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        refreshGate.Dispose();
        lifetimeCancellation.Dispose();
    }
}
