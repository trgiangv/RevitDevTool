using DevTools.Daemon.Mcp.Tools;
using DevTools.Mcp.Routing.Catalog;
using Microsoft.Extensions.Logging;

namespace DevTools.Daemon.Mcp;

public sealed class HostCatalogCoordinator
{
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly Func<CancellationToken, Task> rebuildSnapshotAsync;
    private readonly ILogger<HostCatalogCoordinator>? logger;
    private int refreshRequested;

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
        if (Interlocked.Exchange(ref refreshRequested, 1) == 0)
            _ = RefreshLoopAsync();
    }

    public async Task RebuildSnapshotAsync(CancellationToken ct = default)
    {
        await refreshGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await rebuildSnapshotAsync(ct).ConfigureAwait(false);
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

    private async Task RefreshLoopAsync()
    {
        await refreshGate.WaitAsync().ConfigureAwait(false);
        try
        {
            while (Interlocked.Exchange(ref refreshRequested, 0) != 0)
            {
                try
                {
                    await rebuildSnapshotAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Catalog refresh failed.");
                }
            }
        }
        finally
        {
            refreshGate.Release();
        }
    }
}
