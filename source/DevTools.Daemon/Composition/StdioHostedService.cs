using DevTools.Mcp.Server.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ZLogger;

namespace DevTools.Daemon.Composition;

internal sealed class StdioHostedService(
    McpEngine engine,
    IHostApplicationLifetime lifetime,
    ILoggerFactory loggerFactory,
    IServiceProvider appServices,
    ILogger<StdioHostedService> logger) : BackgroundService
{
    private const string TransportName = "StdioDirect";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var stdin = Console.OpenStandardInput();
            var stdout = Console.OpenStandardOutput();

            var options = McpServerFactory.CreateOptions(
                engine.ToolCollection, engine.PromptCollection, appServices);

            await using var server = McpServer.Create(
                new StreamServerTransport(stdin, stdout, TransportName, loggerFactory),
                options,
                loggerFactory,
                appServices);

            await server.RunAsync(stoppingToken).ConfigureAwait(false);
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
