using DevTools.Daemon.Mcp;
using DevTools.Daemon.Mcp.Tools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevTools.Daemon.Hosting;

/// <summary>
/// Runs InstanceManager pipe discovery and wires up CatalogService for dynamic tool refresh.
/// </summary>
internal sealed class DiscoveryHostedService(
    McpEngine engine,
    ILogger<CatalogService> catalogLogger) : BackgroundService
{
    private CatalogService? _catalogService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _catalogService = new CatalogService(
            engine.InstanceManager,
            engine.ToolCollection,
            engine.PromptCollection,
            engine.ResourceCollection,
            engine.DynamicToolCatalog,
            engine.LocalTools,
            catalogLogger,
            stoppingToken);

        engine.InstanceManager.Changed += _catalogService.RequestRefresh;

        WireRefreshDelegate();

        try
        {
            await engine.InstanceManager.RunDiscoveryAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            engine.InstanceManager.Changed -= _catalogService.RequestRefresh;
        }
    }

    private void WireRefreshDelegate()
    {
        var refreshTool = engine.LocalTools.OfType<RefreshDynamicCatalog>().FirstOrDefault();
        if (refreshTool is not null && _catalogService is not null)
            refreshTool.RefreshDelegate = ct => _catalogService.RebuildCatalogAsync(ct);
    }
}
