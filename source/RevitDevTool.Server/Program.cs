using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RevitDevTool.Server;

var port = 18080;
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] is "--port" or "-p" && int.TryParse(args[i + 1], out var parsed))
    {
        port = parsed;
        break;
    }
}

var bridgeClient = new RevitBridgeClient();
var toolCollection = new McpServerPrimitiveCollection<McpServerTool>();

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services.AddSingleton(bridgeClient);
builder.Services
    .AddMcpServer(options =>
    {
        options.ToolCollection = toolCollection;
    })
    .WithStdioServerTransport();

var host = builder.Build();

var appLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

await bridgeClient.ConnectAsync(port).ConfigureAwait(false);
await RefreshToolsAsync().ConfigureAwait(false);

var pollingToken = appLifetime.ApplicationStopping;
var pollingTask = Task.Run(() => RunPollingAsync(pollingToken), pollingToken);

await host.RunAsync().ConfigureAwait(false);
await pollingTask.ConfigureAwait(false);
return;

async Task RunPollingAsync(CancellationToken cancellationToken)
{
    try
    {
        using var pollTimer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await pollTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                if (!bridgeClient.IsConnected)
                {
                    var reconnected = await bridgeClient.ConnectAsync(port, cancellationToken).ConfigureAwait(false);
                    if (!reconnected)
                        continue;
                }

                await RefreshToolsAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }
    }
    catch (OperationCanceledException)
    {
        // shutdown
    }
}

async Task RefreshToolsAsync()
{
    var tools = await bridgeClient.ListToolsAsync().ConfigureAwait(false);
    if (tools.Count == toolCollection.Count)
        return;

    toolCollection.Clear();
    foreach (var def in tools)
        toolCollection.TryAdd(RevitToolAdapter.ToMcpServerTool(def, bridgeClient));
}
