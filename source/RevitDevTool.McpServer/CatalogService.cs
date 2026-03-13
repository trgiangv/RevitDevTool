using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.McpParser.Models;

namespace RevitDevTool.McpServer;

public sealed class CatalogService
{
    private readonly InstanceManager _instanceManager;
    private readonly McpServerPrimitiveCollection<McpServerTool> _toolCollection;
    private readonly McpServerPrimitiveCollection<McpServerPrompt> _promptCollection;
    private readonly McpServerResourceCollection _resourceCollection;
    private readonly IReadOnlyList<McpServerTool> _localTools;
    private readonly ILogger<CatalogService> _logger;
    private readonly CancellationToken _ct;

    private int _refreshPending;

    public CatalogService(
        InstanceManager instanceManager,
        McpServerPrimitiveCollection<McpServerTool> toolCollection,
        McpServerPrimitiveCollection<McpServerPrompt> promptCollection,
        McpServerResourceCollection resourceCollection,
        IReadOnlyList<McpServerTool> localTools,
        ILogger<CatalogService> logger,
        CancellationToken ct)
    {
        _instanceManager = instanceManager;
        _toolCollection = toolCollection;
        _promptCollection = promptCollection;
        _resourceCollection = resourceCollection;
        _localTools = localTools;
        _logger = logger;
        _ct = ct;
    }

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
                _logger.LogError(ex, "Refresh error");
            }
        }
    }

    private async Task RebuildCatalogAsync()
    {
        var newTools = new Dictionary<string, McpServerTool>(StringComparer.OrdinalIgnoreCase);
        var newPrompts = new Dictionary<string, McpServerPrompt>(StringComparer.OrdinalIgnoreCase);
        var newResources = new List<McpServerResource>();

        foreach (var local in _localTools)
            newTools[local.ProtocolTool.Name] = local;

        foreach (var client in _instanceManager.GetClients())
        {
            if (!client.IsConnected) continue;
            try
            {
                var toolsResponse = await client.RequestAsync(BridgeMethods.ToolsList, ct: _ct).ConfigureAwait(false);
                if (toolsResponse is { IsError: false, Result: { } toolsResult })
                {
                    foreach (var tool in JsonSerializer.Deserialize<List<Tool>>(toolsResult.GetRawText()) ?? [])
                        newTools.TryAdd(tool.Name, new RoutingMcpServerTool(_instanceManager, tool));
                }

                var promptsResponse = await client.RequestAsync(BridgeMethods.PromptsList, ct: _ct).ConfigureAwait(false);
                if (promptsResponse is { IsError: false, Result: { } promptsResult })
                {
                    foreach (var prompt in JsonSerializer.Deserialize<List<Prompt>>(promptsResult.GetRawText()) ?? [])
                        newPrompts.TryAdd(prompt.Name, new RoutingMcpServerPrompt(_instanceManager, prompt));
                }

                var resourcesResponse = await client.RequestAsync(BridgeMethods.ResourcesList, ct: _ct).ConfigureAwait(false);
                if (resourcesResponse is { IsError: false, Result: { } resourcesResult })
                {
                    foreach (var resource in JsonSerializer.Deserialize<List<Resource>>(resourcesResult.GetRawText()) ?? [])
                        newResources.Add(new RoutingMcpServerResource(_instanceManager, resource, null));
                }

                var templatesResponse = await client.RequestAsync(BridgeMethods.ResourceTemplatesList, ct: _ct).ConfigureAwait(false);
                if (templatesResponse is { IsError: false, Result: { } templatesResult })
                {
                    foreach (var template in JsonSerializer.Deserialize<List<ResourceTemplate>>(templatesResult.GetRawText()) ?? [])
                        newResources.Add(new RoutingMcpServerResource(_instanceManager, null, template));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching from {PipeName}", client.PipeName);
            }
        }

        _toolCollection.Clear();
        foreach (var t in newTools.Values) _toolCollection.TryAdd(t);

        _promptCollection.Clear();
        foreach (var p in newPrompts.Values) _promptCollection.TryAdd(p);

        _resourceCollection.Clear();
        foreach (var r in newResources) _resourceCollection.TryAdd(r);
    }
}
