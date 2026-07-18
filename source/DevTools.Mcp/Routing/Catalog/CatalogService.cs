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
    private readonly Dictionary<string, HostCatalogSnapshot> _hostSnapshots = new(StringComparer.OrdinalIgnoreCase);

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

        var connectedSessions = instanceManager.Sessions
            .Where(session => session.IsConnected)
            .ToArray();
        var connectedPipeNames = connectedSessions
            .Select(session => session.Instance.PipeName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var session in connectedSessions)
        {
            token.ThrowIfCancellationRequested();
            var snapshot = await FetchSessionPrimitivesAsync(session, token).ConfigureAwait(false);
            if (snapshot is not null)
                _hostSnapshots[session.Instance.PipeName] = snapshot;
        }

        foreach (var pipeName in _hostSnapshots.Keys.Where(pipe => !connectedPipeNames.Contains(pipe)).ToArray())
            _hostSnapshots.Remove(pipeName);

        foreach (var snapshot in _hostSnapshots.Values)
            AddSnapshot(snapshot, newTools, newPrompts, newResources,
                dynamicToolRegistrations, dynamicResourceRegistrations, dynamicPromptRegistrations);

        dynamicToolCatalog.ReplaceSnapshot(dynamicToolRegistrations);
        dynamicResourceCatalog.ReplaceSnapshot(dynamicResourceRegistrations);
        dynamicPromptCatalog.ReplaceSnapshot(dynamicPromptRegistrations);
        ApplySnapshot(toolCollection, newTools.Values);
        ApplySnapshot(promptCollection, newPrompts.Values);
        ApplySnapshot(resourceCollection, newResources);
    }

    private async Task<HostCatalogSnapshot?> FetchSessionPrimitivesAsync(
        IHostMcpSession session,
        CancellationToken token)
    {
        try
        {
            var toolsTask = session.ListToolsAsync(token);
            var promptsTask = session.ListPromptsAsync(token);
            var resourcesTask = session.ListResourcesAsync(token);
            var templatesTask = session.ListResourceTemplatesAsync(token);

            await Task.WhenAll(toolsTask, promptsTask, resourcesTask, templatesTask).ConfigureAwait(false);
            var snapshot = HostCatalogSnapshot.Create(
                session.Instance,
                await toolsTask.ConfigureAwait(false),
                await promptsTask.ConfigureAwait(false),
                await resourcesTask.ConfigureAwait(false),
                await templatesTask.ConfigureAwait(false));

            return snapshot;
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Error fetching from {session.Instance.PipeName}");
            return null;
        }
    }

    private void AddSnapshot(
        HostCatalogSnapshot snapshot,
        Dictionary<string, McpServerTool> tools,
        Dictionary<string, McpServerPrompt> prompts,
        List<McpServerResource> resources,
        List<DynamicToolCatalogEntry> dynamicToolRegistrations,
        List<DynamicResourceCatalogEntry> dynamicResourceRegistrations,
        List<DynamicPromptCatalogEntry> dynamicPromptRegistrations)
    {
        var instance = ToLegacyInstanceInfo(snapshot.Instance);

        foreach (var clientTool in snapshot.Tools)
        {
            var tool = clientTool.ProtocolTool;
            var key = tool.Name;
            if (!tools.TryAdd(key, new RoutingMcpServerTool(instanceManager, tool)))
            {
                logger.ZLogDebug($"Tool '{key}' already registered, skipping duplicate from {snapshot.Instance.HostApp}_{snapshot.Instance.VersionNumber} (use call_dynamic_tool with hostInstanceId).");
            }
            dynamicToolRegistrations.Add(new DynamicToolCatalogEntry(tool, instance, snapshot.Instance.PipeName));
        }

        foreach (var clientPrompt in snapshot.Prompts)
        {
            var prompt = clientPrompt.ProtocolPrompt;
            var key = prompt.Name;
            if (!prompts.TryAdd(key, new RoutingMcpServerPrompt(instanceManager, prompt)))
            {
                logger.ZLogDebug($"Prompt '{key}' already registered, skipping duplicate from {snapshot.Instance.HostApp}_{snapshot.Instance.VersionNumber}.");
            }
            dynamicPromptRegistrations.Add(new DynamicPromptCatalogEntry(
                key, prompt.Description, prompt, instance, snapshot.Instance.PipeName));
        }

        foreach (var clientResource in snapshot.Resources)
        {
            var resource = clientResource.ProtocolResource;
            resources.Add(new RoutingMcpServerResource(instanceManager, resource, null));
            dynamicResourceRegistrations.Add(new DynamicResourceCatalogEntry(
                resource.Uri, resource.Name, resource.Description, resource.MimeType, instance, snapshot.Instance.PipeName));
        }

        foreach (var clientTemplate in snapshot.ResourceTemplates)
        {
            var template = clientTemplate.ProtocolResourceTemplate;
            resources.Add(new RoutingMcpServerResource(instanceManager, null, template));
            dynamicResourceRegistrations.Add(new DynamicResourceCatalogEntry(
                template.UriTemplate, template.Name, template.Description, template.MimeType, instance, snapshot.Instance.PipeName));
        }
    }

    private static InstanceInfo ToLegacyInstanceInfo(HostInstanceDescriptor instance) =>
        new()
        {
            ProcessId = instance.ProcessId,
            HostApp = instance.HostApp,
            VersionNumber = instance.VersionNumber
        };

    private static void ApplySnapshot<T>(McpServerPrimitiveCollection<T> collection, IEnumerable<T> items)
        where T : IMcpServerPrimitive
    {
        collection.Clear();
        foreach (var item in items)
            collection.TryAdd(item);
    }
}
