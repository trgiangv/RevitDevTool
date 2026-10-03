using DevTools.Daemon.Mcp.Processes;
using Microsoft.Extensions.Hosting;

namespace DevTools.Daemon.Composition;

internal sealed class DiscoveryHostedService(IProcessSessions sessions) : BackgroundService
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
