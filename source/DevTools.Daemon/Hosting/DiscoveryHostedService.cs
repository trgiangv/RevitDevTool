using DevTools.Mcp.Client;
using Microsoft.Extensions.Hosting;

namespace DevTools.Daemon.Hosting;

/// <summary>
/// Runs host MCP pipe discovery and ConnectedHostCatalog hydration.
/// </summary>
internal sealed class DiscoveryHostedService(IHostDiscovery discovery) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await discovery.RunAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
