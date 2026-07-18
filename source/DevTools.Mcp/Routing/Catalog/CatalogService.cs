using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ZLogger;
using DevTools.Mcp.Routing.Broker;
using DevTools.Mcp.Routing.Native;
namespace DevTools.Mcp.Routing.Catalog;

public sealed class CatalogService(
    IInstanceManager instanceManager,
    McpServerPrimitiveCollection<McpServerTool> toolCollection,
    McpServerPrimitiveCollection<McpServerPrompt> promptCollection,
    McpServerResourceCollection resourceCollection,
    BrokerCatalogIndex brokerCatalog,
    bool nativeSurface,
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
        var newHostSnapshots = new Dictionary<string, HostCatalogSnapshot>(_hostSnapshots, StringComparer.OrdinalIgnoreCase);
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
            token.ThrowIfCancellationRequested();
            if (snapshot is not null)
                newHostSnapshots[session.Instance.PipeName] = snapshot;
        }

        foreach (var pipeName in newHostSnapshots.Keys.Where(pipe => !connectedPipeNames.Contains(pipe)).ToArray())
            newHostSnapshots.Remove(pipeName);

        token.ThrowIfCancellationRequested();
        brokerCatalog.ReplaceSnapshots(newHostSnapshots.Values);
        if (nativeSurface)
        {
            foreach (var snapshot in newHostSnapshots.Values)
                AddNativeSnapshot(snapshot, connectedSessions, newTools, newPrompts, newResources);
            ApplySnapshot(toolCollection, newTools.Values);
            ApplySnapshot(promptCollection, newPrompts.Values);
            ApplySnapshot(resourceCollection, newResources);
        }

        _hostSnapshots.Clear();
        foreach (var pair in newHostSnapshots)
            _hostSnapshots[pair.Key] = pair.Value;
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
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Error fetching from {session.Instance.PipeName}");
            return null;
        }
    }

    private static void AddNativeSnapshot(
        HostCatalogSnapshot snapshot,
        IReadOnlyCollection<IHostMcpSession> sessions,
        Dictionary<string, McpServerTool> tools,
        Dictionary<string, McpServerPrompt> prompts,
        List<McpServerResource> resources)
    {
        var session = sessions.SingleOrDefault(item => item.Instance.ProcessId == snapshot.Instance.ProcessId);
        if (session is null)
            return;

        foreach (var clientTool in snapshot.Tools)
        {
            var tool = clientTool.ProtocolTool;
            var proxy = new NativeHostToolProxy(session, tool);
            tools[proxy.ProtocolTool.Name] = proxy;
        }

        foreach (var clientPrompt in snapshot.Prompts)
        {
            var prompt = clientPrompt.ProtocolPrompt;
            var proxy = new NativeHostPromptProxy(session, prompt);
            prompts[proxy.ProtocolPrompt.Name] = proxy;
        }

        foreach (var clientResource in snapshot.Resources)
        {
            var resource = clientResource.ProtocolResource;
            resources.Add(new NativeHostResourceProxy(session, resource, null));
        }

        foreach (var clientTemplate in snapshot.ResourceTemplates)
        {
            var template = clientTemplate.ProtocolResourceTemplate;
            resources.Add(new NativeHostResourceProxy(session, null, template));
        }
    }

    private static void ApplySnapshot<T>(McpServerPrimitiveCollection<T> collection, IEnumerable<T> items)
        where T : IMcpServerPrimitive
    {
        collection.Clear();
        foreach (var item in items)
            collection.TryAdd(item);
    }
}
