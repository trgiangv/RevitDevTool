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
    DynamicResourceCatalog dynamicResourceCatalog,
    DynamicPromptCatalog dynamicPromptCatalog,
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
        var dynamicToolRegistrations = new List<DynamicToolCatalogEntry>();
        var dynamicResourceRegistrations = new List<DynamicResourceCatalogEntry>();
        var dynamicPromptRegistrations = new List<DynamicPromptCatalogEntry>();

        foreach (var local in localTools)
            newTools[local.ProtocolTool.Name] = local;

        foreach (var instance in instanceManager.GetInstances())
        {
            if (instanceManager.GetByProcessId(instance.ProcessId) is not { IsConnected: true } client)
                continue;

            token.ThrowIfCancellationRequested();
            await FetchClientPrimitivesAsync(client, newTools, newPrompts, newResources,
                    dynamicToolRegistrations, dynamicResourceRegistrations, dynamicPromptRegistrations, token)
                .ConfigureAwait(false);
        }

        dynamicToolCatalog.ReplaceSnapshot(dynamicToolRegistrations);
        dynamicResourceCatalog.ReplaceSnapshot(dynamicResourceRegistrations);
        dynamicPromptCatalog.ReplaceSnapshot(dynamicPromptRegistrations);
        ApplySnapshot(toolCollection, newTools.Values);
        ApplySnapshot(promptCollection, newPrompts.Values);
        ApplySnapshot(resourceCollection, newResources);
    }

    private async Task FetchClientPrimitivesAsync(
        IHostBridgeClient client,
        Dictionary<string, McpServerTool> tools,
        Dictionary<string, McpServerPrompt> prompts,
        List<McpServerResource> resources,
        List<DynamicToolCatalogEntry> dynamicToolRegs,
        List<DynamicResourceCatalogEntry> dynamicResourceRegs,
        List<DynamicPromptCatalogEntry> dynamicPromptRegs,
        CancellationToken token)
    {
        try
        {
            await FetchToolsAsync(client, tools, dynamicToolRegs, token).ConfigureAwait(false);
            await FetchPromptsAsync(client, prompts, dynamicPromptRegs, token).ConfigureAwait(false);
            await FetchResourcesAsync(client, resources, dynamicResourceRegs, token).ConfigureAwait(false);
            await FetchResourceTemplatesAsync(client, resources, dynamicResourceRegs, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Error fetching from {client.PipeName}");
        }
    }

    private async Task FetchToolsAsync(
        IHostBridgeClient client,
        Dictionary<string, McpServerTool> tools,
        List<DynamicToolCatalogEntry> dynamicRegistrations,
        CancellationToken token)
    {
        var response = await client.RequestAsync(McpBridgeMethods.ToolsList, ct: token).ConfigureAwait(false);
        foreach (var tool in DeserializeResult<Tool>(response))
        {
            var key = tool.Name;
            if (!tools.TryAdd(key, new RoutingMcpServerTool(instanceManager, tool)))
            {
                logger.ZLogDebug($"Tool '{key}' already registered, skipping duplicate from {client.Info.HostApp}_{client.Info.VersionNumber} (use call_dynamic_tool with hostInstanceId).");
            }
            dynamicRegistrations.Add(new DynamicToolCatalogEntry(tool, client.Info, client.PipeName));
        }
    }

    private async Task FetchPromptsAsync(
        IHostBridgeClient client,
        Dictionary<string, McpServerPrompt> prompts,
        List<DynamicPromptCatalogEntry> dynamicPromptRegs,
        CancellationToken token)
    {
        var response = await client.RequestAsync(McpBridgeMethods.PromptsList, ct: token).ConfigureAwait(false);
        foreach (var prompt in DeserializeResult<Prompt>(response))
        {
            var key = prompt.Name;
            if (!prompts.TryAdd(key, new RoutingMcpServerPrompt(instanceManager, prompt)))
            {
                logger.ZLogDebug($"Prompt '{key}' already registered, skipping duplicate from {client.Info.HostApp}_{client.Info.VersionNumber}.");
            }
            dynamicPromptRegs.Add(new DynamicPromptCatalogEntry(key, prompt.Description, prompt, client.Info, client.PipeName));
        }
    }

    private async Task FetchResourcesAsync(
        IHostBridgeClient client,
        List<McpServerResource> resources,
        List<DynamicResourceCatalogEntry> dynamicResourceRegs,
        CancellationToken token)
    {
        var response = await client.RequestAsync(McpBridgeMethods.ResourcesList, ct: token).ConfigureAwait(false);
        foreach (var resource in DeserializeResult<Resource>(response))
        {
            resources.Add(new RoutingMcpServerResource(instanceManager, resource, null));
            dynamicResourceRegs.Add(new DynamicResourceCatalogEntry(
                resource.Uri, resource.Name, resource.Description, resource.MimeType, client.Info, client.PipeName));
        }
    }

    private async Task FetchResourceTemplatesAsync(
        IHostBridgeClient client,
        List<McpServerResource> resources,
        List<DynamicResourceCatalogEntry> dynamicResourceRegs,
        CancellationToken token)
    {
        var response = await client.RequestAsync(McpBridgeMethods.ResourceTemplatesList, ct: token).ConfigureAwait(false);
        foreach (var template in DeserializeResult<ResourceTemplate>(response))
        {
            resources.Add(new RoutingMcpServerResource(instanceManager, null, template));
            dynamicResourceRegs.Add(new DynamicResourceCatalogEntry(
                template.UriTemplate, template.Name, template.Description, template.MimeType, client.Info, client.PipeName));
        }
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
