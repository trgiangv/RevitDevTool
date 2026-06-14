using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ZLogger;
using DevTools.McpServer;
using DevTools.McpServer.Catalog;
using DevTools.McpServer.Hosting;
using DevTools.McpServer.Tools;

var loggerFactory = LoggerFactory.Create(b => 
    b.AddZLoggerConsole(o => 
        o.LogToStandardErrorThreshold = LogLevel.Trace));

var instanceManagerLogger = loggerFactory.CreateLogger<InstanceManager>();
var catalogLogger = loggerFactory.CreateLogger<CatalogService>();
var instanceManager = new InstanceManager(instanceManagerLogger);
var dynamicToolCatalog = new DynamicToolCatalog();
CatalogService? activeCatalogService = null;

var localTools = new McpServerTool[]
{
    new ListHostInstancesTool(instanceManager),
    new LaunchHostTool(instanceManager),
    new ReadFileInfoTool(),
    new OpenModelTool(instanceManager),
    new ListDynamicTools(dynamicToolCatalog),
    new CallDynamicTool(instanceManager, dynamicToolCatalog),
    new RefreshDynamicCatalog(
        dynamicToolCatalog,
        ct => activeCatalogService?.RebuildCatalogAsync(ct) ?? Task.CompletedTask)
};

var toolCollection = new McpServerPrimitiveCollection<McpServerTool>();
var promptCollection = new McpServerPrimitiveCollection<McpServerPrompt>();
var resourceCollection = new McpServerResourceCollection();
foreach (var tool in localTools) toolCollection.TryAdd(tool);

var gatewayUrl = GetArg(args, "--gateway");
var gatewayToken = GetArg(args, "--token");

if (gatewayUrl is not null)
{
    var mode = new GatewayMode(
        gatewayUrl, gatewayToken,
        instanceManager, toolCollection, promptCollection, resourceCollection,
        dynamicToolCatalog, localTools, catalogLogger, loggerFactory,
        svc => activeCatalogService = svc);
    await mode.RunAsync();
}
else
{
    var mode = new StdioMode(
        args,
        instanceManager, toolCollection, promptCollection, resourceCollection,
        dynamicToolCatalog, localTools, catalogLogger,
        svc => activeCatalogService = svc);
    await mode.RunAsync();
}

await instanceManager.DisposeAsync().ConfigureAwait(false);
return;

static string? GetArg(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }

    return null;
}
