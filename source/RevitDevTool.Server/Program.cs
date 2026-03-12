using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
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
var promptCollection = new McpServerPrimitiveCollection<McpServerPrompt>();
var resourceCollection = new McpServerResourceCollection();
string? catalogSignature = null;

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
        options.PromptCollection = promptCollection;
        options.ResourceCollection = resourceCollection;
    })
    .WithStdioServerTransport();

var host = builder.Build();

var appLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

await bridgeClient.ConnectAsync(port).ConfigureAwait(false);
await RefreshCatalogAsync().ConfigureAwait(false);

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

                await RefreshCatalogAsync().ConfigureAwait(false);
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

async Task RefreshCatalogAsync()
{
    var tools = await bridgeClient.ListToolsAsync().ConfigureAwait(false);
    var prompts = await bridgeClient.ListPromptsAsync().ConfigureAwait(false);
    var (resources, templates) = await bridgeClient.ListResourcesAsync().ConfigureAwait(false);

    var nextSignature = BuildCatalogSignature(tools, prompts, resources, templates);
    if (string.Equals(catalogSignature, nextSignature, StringComparison.Ordinal))
        return;

    catalogSignature = nextSignature;
    toolCollection.Clear();
    foreach (var tool in tools)
        toolCollection.TryAdd(RevitToolAdapter.ToMcpServerTool(tool, tool.Name, bridgeClient));

    promptCollection.Clear();
    foreach (var prompt in prompts)
        promptCollection.TryAdd(RevitPromptAdapter.ToMcpServerPrompt(prompt, prompt.Name, bridgeClient));

    resourceCollection.Clear();
    foreach (var resource in resources)
        resourceCollection.TryAdd(RevitResourceAdapter.ToMcpServerResource(resource, null, resource.Name, bridgeClient));
    foreach (var template in templates)
        resourceCollection.TryAdd(RevitResourceAdapter.ToMcpServerResource(null, template, template.Name, bridgeClient));
}

static string BuildCatalogSignature(
    IReadOnlyList<Tool> tools,
    IReadOnlyList<Prompt> prompts,
    IReadOnlyList<Resource> resources,
    IReadOnlyList<ResourceTemplate> templates)
{
    var toolParts = tools.Select(t => $"tool:{t.Name}:{JsonSerializer.Serialize(t.InputSchema, McpJsonUtilities.DefaultOptions)}");
    var promptParts = prompts.Select(p => $"prompt:{p.Name}:{p.Description}");
    var resourceParts = resources.Select(r => $"resource:{r.Name}:{r.Uri}");
    var templateParts = templates.Select(t => $"template:{t.Name}:{t.UriTemplate}");
    return string.Join("|", toolParts.Concat(promptParts).Concat(resourceParts).Concat(templateParts));
}
