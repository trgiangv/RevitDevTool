using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using DevTools.McpServer.Catalog;
using DevTools.McpServer.Hosting.GateWay;

namespace DevTools.McpServer.Hosting;

internal sealed class GatewayMode(
    string url,
    string? token,
    InstanceManager instanceManager,
    McpServerPrimitiveCollection<McpServerTool> toolCollection,
    McpServerPrimitiveCollection<McpServerPrompt> promptCollection,
    McpServerResourceCollection resourceCollection,
    DynamicToolCatalog dynamicToolCatalog,
    IReadOnlyList<McpServerTool> localTools,
    ILogger<CatalogService> catalogLogger,
    ILoggerFactory loggerFactory,
    Action<CatalogService> onCatalogCreated)
{
    public async Task RunAsync()
    {
        using var shutdownSignal = new GracefulShutdown();
        var ct = shutdownSignal.Token;

        var catalogService = new CatalogService(
            instanceManager, toolCollection, promptCollection, resourceCollection,
            dynamicToolCatalog, localTools, catalogLogger, ct);
        onCatalogCreated(catalogService);
        instanceManager.Changed += catalogService.RequestRefresh;

        var discoveryTask = Task.Run(() => instanceManager.RunDiscoveryAsync(ct), ct);

        var tunnelLogger = loggerFactory.CreateLogger<GatewayTunnelClient>();
        var options = ToolHelpers.ConfigureGatewayOptions(toolCollection, promptCollection, resourceCollection);
        var tunnel = new GatewayTunnelClient(
            new Uri(url),
            token,
            options,
            loggerFactory,
            tunnelLogger);

        try
        {
            await tunnel.RunAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            await tunnel.DisposeAsync().ConfigureAwait(false);
        }

        await discoveryTask.ConfigureAwait(false);
    }
}
