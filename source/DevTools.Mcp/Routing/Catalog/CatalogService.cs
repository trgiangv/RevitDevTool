using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ZLogger;
namespace DevTools.Mcp.Routing.Catalog;

public sealed class CatalogService(
    IInstanceManager instanceManager,
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

        foreach (var instance in instanceManager.GetInstances())
        {
            if (instanceManager.GetByProcessId(instance.ProcessId) is not { IsConnected: true } client)
                continue;

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
        IHostBridgeClient client,
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
        IHostBridgeClient client,
        Dictionary<string, McpServerTool> tools,
        List<DynamicToolRegistration> dynamicRegistrations,
        CancellationToken token)
    {
        var response = await client.RequestAsync(McpBridgeMethods.ToolsList, ct: token).ConfigureAwait(false);
        foreach (var tool in DeserializeResult<Tool>(response))
        {
            var key = tool.Name;
            if (!tools.TryAdd(key, new RoutingMcpServerTool(instanceManager, tool)))
            {
                var namespacedKey = $"{key}@{client.Info.HostApp}_{client.Info.VersionNumber}";
                var namespacedTool = new Tool
                {
                    Name = namespacedKey,
                    Description = tool.Description,
                    InputSchema = tool.InputSchema,
                    Annotations = tool.Annotations
                };
                tools.TryAdd(namespacedKey, new RoutingMcpServerTool(instanceManager, namespacedTool));
                logger.ZLogWarning($"Tool name collision for '{key}', registered as '{namespacedKey}'");
            }
            dynamicRegistrations.Add(new DynamicToolRegistration(tool, client.Info, client.PipeName));
        }
    }

    private async Task FetchPromptsAsync(IHostBridgeClient client, Dictionary<string, McpServerPrompt> prompts, CancellationToken token)
    {
        var response = await client.RequestAsync(McpBridgeMethods.PromptsList, ct: token).ConfigureAwait(false);
        foreach (var prompt in DeserializeResult<Prompt>(response))
        {
            var key = prompt.Name;
            if (!prompts.TryAdd(key, new RoutingMcpServerPrompt(instanceManager, prompt)))
            {
                var namespacedKey = $"{key}@{client.Info.HostApp}_{client.Info.VersionNumber}";
                var namespacedPrompt = new Prompt
                {
                    Name = namespacedKey,
                    Description = prompt.Description,
                    Arguments = prompt.Arguments
                };
                prompts.TryAdd(namespacedKey, new RoutingMcpServerPrompt(instanceManager, namespacedPrompt));
                logger.ZLogWarning($"Prompt name collision for '{key}', registered as '{namespacedKey}'");
            }
        }
    }

    private async Task FetchResourcesAsync(IHostBridgeClient client, List<McpServerResource> resources, CancellationToken token)
    {
        var response = await client.RequestAsync(McpBridgeMethods.ResourcesList, ct: token).ConfigureAwait(false);
        resources.AddRange(DeserializeResult<Resource>(response).Select(resource => new RoutingMcpServerResource(instanceManager, resource, null)));
    }

    private async Task FetchResourceTemplatesAsync(IHostBridgeClient client, List<McpServerResource> resources, CancellationToken token)
    {
        var response = await client.RequestAsync(McpBridgeMethods.ResourceTemplatesList, ct: token).ConfigureAwait(false);
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
