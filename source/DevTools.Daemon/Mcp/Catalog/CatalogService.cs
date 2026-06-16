using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ZLogger;
using DevTools.McpParser.Models;

namespace DevTools.Daemon.Mcp.Catalog;

public sealed class CatalogService(InstanceManager instanceManager, 
    McpServerPrimitiveCollection<McpServerTool> toolCollection, 
    McpServerPrimitiveCollection<McpServerPrompt> promptCollection, 
    McpServerResourceCollection resourceCollection, 
    DynamicToolCatalog dynamicToolCatalog,
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
                logger.ZLogError(ex, $"Refresh error");
            }
        }
    }

    public async Task RebuildCatalogAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
        var token = linked.Token;

        var newTools = new Dictionary<string, McpServerTool>(StringComparer.OrdinalIgnoreCase);
        var newPrompts = new Dictionary<string, McpServerPrompt>(StringComparer.OrdinalIgnoreCase);
        var newResources = new List<McpServerResource>();
        var dynamicRegistrations = new List<DynamicToolRegistration>();

        foreach (var local in localTools)
            newTools[local.ProtocolTool.Name] = local;

        foreach (var client in instanceManager.GetClients().Where(client => client.IsConnected))
        {
            token.ThrowIfCancellationRequested();
            await FetchClientPrimitivesAsync(client, newTools, newPrompts, newResources, dynamicRegistrations, token)
                .ConfigureAwait(false);
        }

        dynamicToolCatalog.ReplaceSnapshot(dynamicRegistrations);
        ApplySnapshot(toolCollection, newTools.Values);
        ApplySnapshot(promptCollection, newPrompts.Values);
        ApplySnapshot(resourceCollection, newResources);
    }

    private async Task FetchClientPrimitivesAsync(
        HostBridgeClient client,
        Dictionary<string, McpServerTool> tools,
        Dictionary<string, McpServerPrompt> prompts,
        List<McpServerResource> resources,
        List<DynamicToolRegistration> dynamicRegistrations,
        CancellationToken token)
    {
        try
        {
            await FetchToolsAsync(client, tools, dynamicRegistrations, token).ConfigureAwait(false);
            await FetchPromptsAsync(client, prompts, token).ConfigureAwait(false);
            await FetchResourcesAsync(client, resources, token).ConfigureAwait(false);
            await FetchResourceTemplatesAsync(client, resources, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Error fetching from {client.PipeName}");
        }
    }

    private async Task FetchToolsAsync(
        HostBridgeClient client,
        Dictionary<string, McpServerTool> tools,
        List<DynamicToolRegistration> dynamicRegistrations,
        CancellationToken token)
    {
        var response = await client.RequestAsync(BridgeMethods.ToolsList, ct: token).ConfigureAwait(false);
        foreach (var tool in DeserializeResult<Tool>(response))
        {
            tools.TryAdd(tool.Name, new RoutingMcpServerTool(instanceManager, tool));
            if (client.Info is not null)
                dynamicRegistrations.Add(new DynamicToolRegistration(tool, client.Info, client.PipeName));
        }
    }

    private async Task FetchPromptsAsync(HostBridgeClient client, Dictionary<string, McpServerPrompt> prompts, CancellationToken token)
    {
        var response = await client.RequestAsync(BridgeMethods.PromptsList, ct: token).ConfigureAwait(false);
        foreach (var prompt in DeserializeResult<Prompt>(response))
            prompts.TryAdd(prompt.Name, new RoutingMcpServerPrompt(instanceManager, prompt));
    }

    private async Task FetchResourcesAsync(HostBridgeClient client, List<McpServerResource> resources, CancellationToken token)
    {
        var response = await client.RequestAsync(BridgeMethods.ResourcesList, ct: token).ConfigureAwait(false);
        resources.AddRange(DeserializeResult<Resource>(response).Select(resource => new RoutingMcpServerResource(instanceManager, resource, null)));
    }

    private async Task FetchResourceTemplatesAsync(HostBridgeClient client, List<McpServerResource> resources, CancellationToken token)
    {
        var response = await client.RequestAsync(BridgeMethods.ResourceTemplatesList, ct: token).ConfigureAwait(false);
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
