using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RevitDevTool.McpServer;
using RevitDevTool.McpServer.Tools;

var loggerFactory = LoggerFactory.Create(b => b.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace));
var instanceManagerLogger = loggerFactory.CreateLogger<InstanceManager>();
var catalogLogger = loggerFactory.CreateLogger<CatalogService>();
var instanceManager = new InstanceManager(instanceManagerLogger);

var localTools = new McpServerTool[]
{
    new ListRevitInstancesTool(instanceManager),
    new LaunchRevitTool(),
    new ReadRevitFileInfoTool(),
    new OpenRevitModelTool(instanceManager)
};

var toolCollection = new McpServerPrimitiveCollection<McpServerTool>();
var promptCollection = new McpServerPrimitiveCollection<McpServerPrompt>();
var resourceCollection = new McpServerResourceCollection();
foreach (var tool in localTools) toolCollection.TryAdd(tool);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services.AddMcpServer(options =>
{
    options.ToolCollection = toolCollection;
    options.PromptCollection = promptCollection;
    options.ResourceCollection = resourceCollection;
}).WithStdioServerTransport();

var host = builder.Build();
var appLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
var ct = appLifetime.ApplicationStopping;

var catalogService = new CatalogService(
    instanceManager,
    toolCollection,
    promptCollection,
    resourceCollection,
    localTools,
    catalogLogger,
    ct);
instanceManager.Changed += catalogService.RequestRefresh;

var discoveryTask = Task.Run(() => instanceManager.RunDiscoveryAsync(ct), ct);

await host.RunAsync().ConfigureAwait(false);
await discoveryTask.ConfigureAwait(false);
await instanceManager.DisposeAsync().ConfigureAwait(false);
