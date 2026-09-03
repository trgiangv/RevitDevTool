using DevTools.Mcp.Client;
using Microsoft.Extensions.Hosting;

namespace DevTools.Daemon.Composition;

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
