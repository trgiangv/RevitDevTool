using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ZLogger;
using DevTools.McpServer;
using DevTools.McpServer.Tools;

var loggerFactory = LoggerFactory.Create(b => b.AddZLoggerConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace));
var instanceManagerLogger = loggerFactory.CreateLogger<InstanceManager>();
var catalogLogger = loggerFactory.CreateLogger<CatalogService>();
var instanceManager = new InstanceManager(instanceManagerLogger);

var localTools = new McpServerTool[]
{
    new ListHostInstancesTool(instanceManager),
    new LaunchHostTool(instanceManager),
    new ReadFileInfoTool(),
    new OpenModelTool(instanceManager)
};

var toolCollection = new McpServerPrimitiveCollection<McpServerTool>();
var promptCollection = new McpServerPrimitiveCollection<McpServerPrompt>();
var resourceCollection = new McpServerResourceCollection();
foreach (var tool in localTools) toolCollection.TryAdd(tool);

var gatewayUrl = GetArg(args, "--gateway");
var gatewayToken = GetArg(args, "--token");

if (gatewayUrl is not null)
{
    await RunGatewayModeAsync(gatewayUrl, gatewayToken);
}
else
{
    await RunStdioModeAsync();
}

await instanceManager.DisposeAsync().ConfigureAwait(false);
return;

async Task RunStdioModeAsync()
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.ClearProviders();
    builder.Logging.AddZLoggerConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

    builder.Services.AddMcpServer(options =>
    {
        options.ToolCollection = toolCollection;
        options.PromptCollection = promptCollection;
        options.ResourceCollection = resourceCollection;
    })
    .WithStdioServerTransport();

    builder.Services.AddSingleton(instanceManager);
    builder.Services.AddSingleton(catalogLogger);

    var host = builder.Build();
    var ct = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

    var catalogService = new CatalogService(instanceManager, toolCollection, promptCollection, resourceCollection, localTools, catalogLogger, ct);
    instanceManager.Changed += catalogService.RequestRefresh;

    var discoveryTask = Task.Run(() => instanceManager.RunDiscoveryAsync(ct), ct);

    await host.RunAsync().ConfigureAwait(false);
    await discoveryTask.ConfigureAwait(false);
}

async Task RunGatewayModeAsync(string url, string? token)
{
    using var shutdownSignal = new GracefulShutdown();
    var ct = shutdownSignal.Token;

    var catalogService = new CatalogService(instanceManager, toolCollection, promptCollection, resourceCollection, localTools, catalogLogger, ct);
    instanceManager.Changed += catalogService.RequestRefresh;

    var discoveryTask = Task.Run(() => instanceManager.RunDiscoveryAsync(ct), ct);

    var tunnelLogger = loggerFactory.CreateLogger<GatewayTunnelClient>();
    var tunnel = new GatewayTunnelClient(
        new Uri(url),
        token,
        new McpServerOptions
        {
            ToolCollection = toolCollection,
            PromptCollection = promptCollection,
            ResourceCollection = resourceCollection
        },
        loggerFactory,
        tunnelLogger);

    try
    {
        await tunnel.RunAsync(ct).ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
    }
    finally
    {
        await tunnel.DisposeAsync().ConfigureAwait(false);
    }

    await discoveryTask.ConfigureAwait(false);
}

static string? GetArg(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }

    return null;
}

internal sealed class GracefulShutdown : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    public CancellationToken Token => _cts.Token;

    public GracefulShutdown() => Console.CancelKeyPress += OnCancelKeyPress;

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _cts.Cancel();
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _cts.Dispose();
    }
}
