using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Contracts;
using RevitDevTool.Server;
using RevitDevTool.Server.Tools;

var instanceManager = new InstanceManager();
var refreshPending = 0;

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

instanceManager.Changed += RequestCatalogRefresh;

var discoveryTask = Task.Run(() => instanceManager.RunDiscoveryAsync(ct), ct);

await host.RunAsync().ConfigureAwait(false);
await discoveryTask.ConfigureAwait(false);
await instanceManager.DisposeAsync().ConfigureAwait(false);

return;

void RequestCatalogRefresh()
{
    if (Interlocked.Exchange(ref refreshPending, 1) == 0)
        _ = RefreshLoopAsync();
}

async Task RefreshLoopAsync()
{
    while (Interlocked.Exchange(ref refreshPending, 0) != 0)
    {
        try
        {
            await RebuildCatalogAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[Catalog] Refresh error: {ex.Message}").ConfigureAwait(false);
        }
    }
}

async Task RebuildCatalogAsync()
{
    var newTools = new Dictionary<string, McpServerTool>(StringComparer.OrdinalIgnoreCase);
    var newPrompts = new Dictionary<string, McpServerPrompt>(StringComparer.OrdinalIgnoreCase);
    var newResources = new List<McpServerResource>();

    foreach (var local in localTools)
        newTools[local.ProtocolTool.Name] = local;

    foreach (var client in instanceManager.GetClients())
    {
        if (!client.IsConnected) continue;
        try
        {
            var toolsResponse = await client.RequestAsync(BridgeMethods.ToolsList, ct: ct).ConfigureAwait(false);
            if (toolsResponse is { IsError: false, Result: { } toolsResult })
            {
                foreach (var tool in JsonSerializer.Deserialize<List<Tool>>(toolsResult.GetRawText()) ?? [])
                    newTools.TryAdd(tool.Name, new RoutingMcpServerTool(instanceManager, tool));
            }

            var promptsResponse = await client.RequestAsync(BridgeMethods.PromptsList, ct: ct).ConfigureAwait(false);
            if (promptsResponse is { IsError: false, Result: { } promptsResult })
            {
                foreach (var prompt in JsonSerializer.Deserialize<List<Prompt>>(promptsResult.GetRawText()) ?? [])
                    newPrompts.TryAdd(prompt.Name, new RoutingMcpServerPrompt(instanceManager, prompt));
            }

            var resourcesResponse = await client.RequestAsync(BridgeMethods.ResourcesList, ct: ct).ConfigureAwait(false);
            if (resourcesResponse is { IsError: false, Result: { } resourcesResult })
            {
                foreach (var resource in JsonSerializer.Deserialize<List<Resource>>(resourcesResult.GetRawText()) ?? [])
                    newResources.Add(new RoutingMcpServerResource(instanceManager, resource, null));
            }

            var templatesResponse = await client.RequestAsync(BridgeMethods.ResourceTemplatesList, ct: ct).ConfigureAwait(false);
            if (templatesResponse is { IsError: false, Result: { } templatesResult })
            {
                foreach (var template in JsonSerializer.Deserialize<List<ResourceTemplate>>(templatesResult.GetRawText()) ?? [])
                    newResources.Add(new RoutingMcpServerResource(instanceManager, null, template));
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[Catalog] Error fetching from {client.PipeName}: {ex.Message}").ConfigureAwait(false);
        }
    }

    toolCollection.Clear();
    foreach (var t in newTools.Values) toolCollection.TryAdd(t);

    promptCollection.Clear();
    foreach (var p in newPrompts.Values) promptCollection.TryAdd(p);

    resourceCollection.Clear();
    foreach (var r in newResources) resourceCollection.TryAdd(r);
}