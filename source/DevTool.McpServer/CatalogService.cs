using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using DevTool.McpParser.Models;

namespace DevTool.McpServer;

public sealed class CatalogService(InstanceManager instanceManager, 
    McpServerPrimitiveCollection<McpServerTool> toolCollection, 
    McpServerPrimitiveCollection<McpServerPrompt> promptCollection, 
    McpServerResourceCollection resourceCollection, 
    IReadOnlyList<McpServerTool> localTools, 
    ILogger<CatalogService> logger, 
    CancellationToken ct)
{
    private int _refreshPending;

    public void RequestRefresh()
    {
        if (Interlocked.Exchange(ref _refreshPending, 1) == 0)
            _ = RefreshLoopAsync();
    }

    private async Task RefreshLoopAsync()
    {
        while (Interlocked.Exchange(ref _refreshPending, 0) != 0)
        {
            try
            {
                await RebuildCatalogAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Refresh error");
            }
        }
    }

    private async Task RebuildCatalogAsync()
    {
        var newTools = new Dictionary<string, McpServerTool>(StringComparer.OrdinalIgnoreCase);
        var newPrompts = new Dictionary<string, McpServerPrompt>(StringComparer.OrdinalIgnoreCase);
        var newResources = new List<McpServerResource>();

        foreach (var local in localTools)
            newTools[local.ProtocolTool.Name] = local;

        foreach (var client in instanceManager.GetClients().Where(client => client.IsConnected))
        {
            await FetchClientPrimitivesAsync(client, newTools, newPrompts, newResources).ConfigureAwait(false);
        }

        ApplySnapshot(toolCollection, newTools.Values);
        ApplySnapshot(promptCollection, newPrompts.Values);
        ApplySnapshot(resourceCollection, newResources);
    }

    private async Task FetchClientPrimitivesAsync(
        RevitBridgeClient client,
        Dictionary<string, McpServerTool> tools,
        Dictionary<string, McpServerPrompt> prompts,
        List<McpServerResource> resources)
    {
        try
        {
            await FetchToolsAsync(client, tools).ConfigureAwait(false);
            await FetchPromptsAsync(client, prompts).ConfigureAwait(false);
            await FetchResourcesAsync(client, resources).ConfigureAwait(false);
            await FetchResourceTemplatesAsync(client, resources).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error fetching from {PipeName}", client.PipeName);
        }
    }

    private async Task FetchToolsAsync(RevitBridgeClient client, Dictionary<string, McpServerTool> tools)
    {
        var response = await client.RequestAsync(BridgeMethods.ToolsList, ct: ct).ConfigureAwait(false);
        foreach (var tool in DeserializeResult<Tool>(response))
            tools.TryAdd(tool.Name, new RoutingMcpServerTool(instanceManager, tool));
    }

    private async Task FetchPromptsAsync(RevitBridgeClient client, Dictionary<string, McpServerPrompt> prompts)
    {
        var response = await client.RequestAsync(BridgeMethods.PromptsList, ct: ct).ConfigureAwait(false);
        foreach (var prompt in DeserializeResult<Prompt>(response))
            prompts.TryAdd(prompt.Name, new RoutingMcpServerPrompt(instanceManager, prompt));
    }

    private async Task FetchResourcesAsync(RevitBridgeClient client, List<McpServerResource> resources)
    {
        var response = await client.RequestAsync(BridgeMethods.ResourcesList, ct: ct).ConfigureAwait(false);
        resources.AddRange(DeserializeResult<Resource>(response).Select(resource => new RoutingMcpServerResource(instanceManager, resource, null)));
    }

    private async Task FetchResourceTemplatesAsync(RevitBridgeClient client, List<McpServerResource> resources)
    {
        var response = await client.RequestAsync(BridgeMethods.ResourceTemplatesList, ct: ct).ConfigureAwait(false);
        resources.AddRange(DeserializeResult<ResourceTemplate>(response).Select(template => new RoutingMcpServerResource(instanceManager, null, template)));
    }

    private static List<T> DeserializeResult<T>(BridgeMessage response)
    {
        if (response is { IsError: false, Result: { } result })
            return JsonSerializer.Deserialize<List<T>>(result.GetRawText()) ?? [];
        return [];
    }

    private static void ApplySnapshot<T>(McpServerPrimitiveCollection<T> collection, IEnumerable<T> items)
        where T : IMcpServerPrimitive
    {
        collection.Clear();
        foreach (var item in items) 
            collection.TryAdd(item);
    }
}
