using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ZLogger;
using DevTools.McpServer.Catalog;

namespace DevTools.McpServer.Hosting;

internal sealed class StdioMode(
    string[] args,
    InstanceManager instanceManager,
    McpServerPrimitiveCollection<McpServerTool> toolCollection,
    McpServerPrimitiveCollection<McpServerPrompt> promptCollection,
    McpServerResourceCollection resourceCollection,
    DynamicToolCatalog dynamicToolCatalog,
    IReadOnlyList<McpServerTool> localTools,
    ILogger<CatalogService> catalogLogger,
    Action<CatalogService> onCatalogCreated)
{
    public async Task RunAsync()
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddZLoggerConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

        builder.Services.AddMcpServer(options =>
        {
            options.ConfigureDynamicCatalog();
            options.ToolCollection = toolCollection;
            options.PromptCollection = promptCollection;
            options.ResourceCollection = resourceCollection;
        })
        .WithStdioServerTransport();

        builder.Services.AddSingleton(instanceManager);
        builder.Services.AddSingleton(catalogLogger);

        var host = builder.Build();
        var ct = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

        var catalogService = new CatalogService(
            instanceManager, toolCollection, promptCollection, resourceCollection,
            dynamicToolCatalog, localTools, catalogLogger, ct);
        onCatalogCreated(catalogService);
        instanceManager.Changed += catalogService.RequestRefresh;

        var discoveryTask = Task.Run(() => instanceManager.RunDiscoveryAsync(ct), ct);

        await host.RunAsync(token: ct).ConfigureAwait(false);
        await discoveryTask.ConfigureAwait(false);
    }
}
