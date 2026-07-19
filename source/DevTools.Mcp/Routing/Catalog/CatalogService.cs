using System.Diagnostics;
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
    private readonly Dictionary<HostCatalogIdentity, HostCatalogPublication> publications = [];

    public event Action<HostCatalogPublication>? PublicationChanged;

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
        var rebuildTimer = Stopwatch.StartNew();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
        var token = linked.Token;

        var newTools = new Dictionary<string, McpServerTool>(StringComparer.OrdinalIgnoreCase);
        var newPrompts = new Dictionary<string, McpServerPrompt>(StringComparer.OrdinalIgnoreCase);
        var newResources = new List<McpServerResource>();
        foreach (var local in localTools)
            newTools[local.ProtocolTool.Name] = local;

        token.ThrowIfCancellationRequested();
        var connectedSessions = instanceManager.Sessions
            .Where(session => session.IsConnected)
            .ToArray();
        var connectedIdentities = connectedSessions.Select(IdentityOf).ToHashSet();
        var interim = publications
            .Where(pair => connectedIdentities.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var refreshing = new List<HostCatalogPublication>();
        foreach (var session in connectedSessions)
        {
            var identity = IdentityOf(session);
            if (interim.ContainsKey(identity))
                continue;

            var publication = new HostCatalogPublication(
                identity,
                session.Instance,
                HostCatalogState.Refreshing,
                null,
                null,
                null,
                null);
            interim.Add(identity, publication);
            refreshing.Add(publication);
        }

        InstallPublications(interim.Values);
        foreach (var publication in refreshing)
            PublicationChanged?.Invoke(publication);

        var fetched = await Task.WhenAll(connectedSessions.Select(session =>
                FetchPublicationAsync(session, interim.GetValueOrDefault(IdentityOf(session)), token)))
            .ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        InstallPublications(fetched);

        if (nativeSurface)
        {
            foreach (var publication in fetched.Where(publication =>
                         publication.State is HostCatalogState.Ready or HostCatalogState.Stale &&
                         publication.Snapshot is not null))
                AddNativeSnapshot(publication.Snapshot!, connectedSessions, newTools, newPrompts, newResources);
            ApplySnapshot(toolCollection, newTools.Values);
            ApplySnapshot(promptCollection, newPrompts.Values);
            ApplySnapshot(resourceCollection, newResources);
        }

        foreach (var publication in fetched)
            PublicationChanged?.Invoke(publication);

        LogAggregatePublication(fetched, rebuildTimer.Elapsed);
    }

    private async Task<HostCatalogPublication> FetchPublicationAsync(
        IHostMcpSession session,
        HostCatalogPublication? prior,
        CancellationToken token)
    {
        var timer = Stopwatch.StartNew();
        HostCatalogPublication publication;
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

            publication = new HostCatalogPublication(
                IdentityOf(session),
                session.Instance,
                HostCatalogState.Ready,
                snapshot,
                DateTimeOffset.UtcNow,
                null,
                null);
        }
        catch (Exception ex)
        {
            var identity = IdentityOf(session);
            var failedAt = DateTimeOffset.UtcNow;
            var errorCode = !session.IsConnected
                ? "host_disconnected"
                : ex is OperationCanceledException
                    ? "catalog_cancelled"
                    : "catalog_fetch_failed";
            publication = prior is { Snapshot: not null } && prior.Identity == identity
                ? prior with
                {
                    State = HostCatalogState.Stale,
                    StaleSince = prior.StaleSince ?? failedAt,
                    LastErrorCode = errorCode
                }
                : new HostCatalogPublication(
                    identity,
                    session.Instance,
                    HostCatalogState.Unavailable,
                    null,
                    null,
                    null,
                    errorCode);

            if (ex is not OperationCanceledException)
                logger.ZLogWarning(ex, $"Catalog fetch exception for {session.Instance.PipeName}");
        }

        logger.ZLogInformation($"Catalog fetch PipeName={session.Instance.PipeName} PID={session.Instance.ProcessId} Generation={session.Generation} DurationMs={timer.ElapsedMilliseconds} State={publication.State} ErrorCode={publication.LastErrorCode}");
        return publication;
    }

    private static HostCatalogIdentity IdentityOf(IHostMcpSession session) =>
        new(session.Instance.PipeName, session.Generation);

    private void InstallPublications(IEnumerable<HostCatalogPublication> replacement)
    {
        var complete = replacement.ToArray();
        brokerCatalog.ReplacePublications(complete);
        publications.Clear();
        foreach (var publication in complete)
            publications[publication.Identity] = publication;
    }

    private void LogAggregatePublication(IReadOnlyCollection<HostCatalogPublication> complete, TimeSpan duration)
    {
        var revision = brokerCatalog.Search(new BrokerSearchRequest(null, null, null, BrokerSearchDetail.Summary, 1)).Revision;
        var primitiveCount = complete
            .Where(publication => publication.State is HostCatalogState.Ready or HostCatalogState.Stale)
            .Where(publication => publication.Snapshot is not null)
            .Sum(publication => publication.Snapshot!.Tools.Count +
                                publication.Snapshot.Prompts.Count +
                                publication.Snapshot.Resources.Count +
                                publication.Snapshot.ResourceTemplates.Count);
        var refreshingCount = complete.Count(publication => publication.State == HostCatalogState.Refreshing);
        var readyCount = complete.Count(publication => publication.State == HostCatalogState.Ready);
        var staleCount = complete.Count(publication => publication.State == HostCatalogState.Stale);
        var unavailableCount = complete.Count(publication => publication.State == HostCatalogState.Unavailable);
        logger.ZLogInformation($"Catalog publication Revision={revision} HostCount={complete.Count} PrimitiveCount={primitiveCount} DurationMs={duration.TotalMilliseconds} RefreshingCount={refreshingCount} ReadyCount={readyCount} StaleCount={staleCount} UnavailableCount={unavailableCount}");
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
