using DevTools.Daemon.Mcp;
using Microsoft.Extensions.Hosting;

namespace DevTools.Daemon.Hosting;

/// <summary>
/// Runs host discovery and wires typed session changes to the single catalog coordinator.
/// </summary>
internal sealed class DiscoveryHostedService(
    HostSessionManager sessionManager,
    HostCatalogCoordinator catalogCoordinator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        sessionManager.SessionsChanged += catalogCoordinator.RequestRefresh;

        try
        {
            await sessionManager.RunAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            sessionManager.SessionsChanged -= catalogCoordinator.RequestRefresh;
        }
    }
}
