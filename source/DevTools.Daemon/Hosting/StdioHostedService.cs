using DevTools.Daemon.Mcp;
using DevTools.Daemon.Mcp.Tools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ZLogger;

namespace DevTools.Daemon.Hosting;

/// <summary>
/// Serves MCP directly over process stdin/stdout.
/// Used when the Daemon is launched with --stdio by an AI client.
/// </summary>
internal sealed class StdioHostedService(
    McpEngine engine,
    IHostApplicationLifetime lifetime,
    ILoggerFactory loggerFactory,
    ILogger<StdioHostedService> logger) : BackgroundService
{
    private const string TransportName = "StdioDirect";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var stdin = Console.OpenStandardInput();
            var stdout = Console.OpenStandardOutput();

            var options = ToolHelpers.ConfigureGatewayOptions(
                engine.ToolCollection, engine.PromptCollection, engine.ResourceCollection);

            await using var server = McpServer.Create(
                new StreamServerTransport(stdin, stdout, TransportName, loggerFactory),
                options);

            var catalogLogger = loggerFactory.CreateLogger<CatalogService>();
            var catalogService = new CatalogService(
                engine.InstanceManager, engine.ToolCollection, engine.PromptCollection,
                engine.ResourceCollection, engine.DynamicToolCatalog, engine.LocalTools,
                catalogLogger, stoppingToken);
            engine.InstanceManager.Changed += catalogService.RequestRefresh;

            var refreshTool = engine.LocalTools.OfType<RefreshDynamicCatalog>().FirstOrDefault();
            refreshTool?.RefreshDelegate = catalogService.RebuildCatalogAsync;

            try
            {
                await server.RunAsync(stoppingToken).ConfigureAwait(false);
            }
            finally
            {
                engine.InstanceManager.Changed -= catalogService.RequestRefresh;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Stdio session ended with error");
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
