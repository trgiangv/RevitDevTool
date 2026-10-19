using Microsoft.Extensions.Hosting;

namespace DevTools.Daemon.Mcp.Processes;

internal sealed class DiscoveryHostedService(IHostConnections sessions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await sessions.RunAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
