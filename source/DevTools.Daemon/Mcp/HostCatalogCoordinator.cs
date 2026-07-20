using System.Collections.Concurrent;
using DevTools.Daemon.Mcp.Tools;
using DevTools.Mcp.Routing.Catalog;
using DevTools.Daemon.Hosting;
using DevTools.Mcp;
using Microsoft.Extensions.Logging;

namespace DevTools.Daemon.Mcp;

public sealed class HostCatalogCoordinator : IAsyncDisposable
{
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly ConcurrentDictionary<HostCatalogIdentity, TaskCompletionSource<HostCatalogState>> firstFetches = new();
    private readonly Lock lifecycleGate = new();
    private readonly Func<CancellationToken, Task> rebuildSnapshotAsync;
    private readonly IInstanceManager? instanceManager;
    private readonly CatalogService? catalogService;
    private readonly ILogger<HostCatalogCoordinator>? logger;
    private int refreshRequested;
    private Task? refreshTask;
    private Task? disposeTask;
    private TaskCompletionSource<bool>? refreshesDrained;
    private int activeRefreshes;
    private bool disposing;

    public HostCatalogCoordinator(McpEngine engine, DaemonSettings settings, ILogger<CatalogService> catalogLogger, ILogger<HostCatalogCoordinator> logger)
    {
        catalogService = new CatalogService(
            engine.InstanceManager,
            engine.ToolCollection,
            engine.PromptCollection,
            engine.ResourceCollection,
            engine.BrokerCatalog,
            settings.McpSurface == McpSurfaceMode.Native,
            engine.LocalTools,
            catalogLogger,
            CancellationToken.None);
        rebuildSnapshotAsync = catalogService.RebuildCatalogAsync;
        instanceManager = engine.InstanceManager;
        this.logger = logger;
        catalogService.PublicationChanged += PublishStatus;
    }

    internal HostCatalogCoordinator(Func<CancellationToken, Task> rebuildSnapshotAsync)
        : this(rebuildSnapshotAsync, null)
    {
    }

    internal HostCatalogCoordinator(
        Func<CancellationToken, Task> rebuildSnapshotAsync,
        IInstanceManager? instanceManager)
    {
        this.rebuildSnapshotAsync = rebuildSnapshotAsync;
        this.instanceManager = instanceManager;
    }

    public void RequestRefresh()
    {
        lock (lifecycleGate)
        {
            if (disposing)
                return;

            ObserveConnectedSessions();
            if (Interlocked.Exchange(ref refreshRequested, 1) == 0)
            {
                BeginRefreshLocked();
                refreshTask = RefreshLoopAsync(lifetimeCancellation.Token);
            }
        }
    }

    public async Task<HostCatalogState> WaitForFirstFetchAsync(
        int processId,
        int generation,
        TimeSpan timeout,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (timeout < Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        IHostMcpSession? session;
        lock (lifecycleGate)
        {
            if (disposing)
                return HostCatalogState.Unavailable;

            session = instanceManager?.GetSession(processId, generation);
        }

        if (session is null)
            return HostCatalogState.Unavailable;

        var identity = new HostCatalogIdentity(session.Instance.PipeName, generation);
        TaskCompletionSource<HostCatalogState> firstFetch;
        lock (lifecycleGate)
        {
            if (disposing)
                return HostCatalogState.Unavailable;

            var current = instanceManager?.GetSession(processId, generation);
            if (current is null || !StringComparer.OrdinalIgnoreCase.Equals(current.Instance.PipeName, identity.PipeName))
                return HostCatalogState.Unavailable;

            firstFetch = firstFetches.GetOrAdd(identity, static _ => CreateFirstFetch());
        }

        using var timeoutCancellation = new CancellationTokenSource();
        timeoutCancellation.CancelAfter(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCancellation.Token);
        try
        {
            return await firstFetch.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return HostCatalogState.Refreshing;
        }
    }

    internal void PublishStatus(HostCatalogIdentity identity, HostCatalogState state)
    {
        if (state is not (HostCatalogState.Ready or HostCatalogState.Stale or HostCatalogState.Unavailable))
            return;

        if (firstFetches.TryGetValue(identity, out var firstFetch))
        {
            firstFetch.TrySetResult(state);
            return;
        }

        if (instanceManager?.Sessions.Any(session => IdentityOf(session) == identity) != true)
            return;

        firstFetches.GetOrAdd(identity, static _ => CreateFirstFetch()).TrySetResult(state);
    }

    private void PublishStatus(HostCatalogPublication publication) =>
        PublishStatus(publication.Identity, publication.State);

    public Task RebuildSnapshotAsync(CancellationToken ct = default)
    {
        lock (lifecycleGate)
        {
            if (disposing)
                return Task.FromCanceled(new CancellationToken(canceled: true));

            BeginRefreshLocked();
        }

        return RebuildSnapshotCoreAsync(ct);
    }

    private async Task RebuildSnapshotCoreAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetimeCancellation.Token);
        var gateHeld = false;
        try
        {
            await refreshGate.WaitAsync(linked.Token).ConfigureAwait(false);
            gateHeld = true;
            await rebuildSnapshotAsync(linked.Token).ConfigureAwait(false);
        }
        finally
        {
            if (gateHeld)
                refreshGate.Release();
            CompleteRefresh();
        }
    }

    internal async Task WaitForIdleAsync(CancellationToken ct)
    {
        while (true)
        {
            await refreshGate.WaitAsync(ct).ConfigureAwait(false);
            refreshGate.Release();
            if (Volatile.Read(ref refreshRequested) == 0)
                return;
        }
    }

    private async Task RefreshLoopAsync(CancellationToken ct)
    {
        var gateHeld = false;
        try
        {
            await refreshGate.WaitAsync(ct).ConfigureAwait(false);
            gateHeld = true;
            while (Interlocked.Exchange(ref refreshRequested, 0) != 0)
            {
                try
                {
                    await rebuildSnapshotAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Catalog refresh failed.");
                }
            }
        }
        finally
        {
            if (gateHeld)
                refreshGate.Release();
            CompleteRefresh();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (lifecycleGate)
        {
            if (disposeTask is not null)
                return new ValueTask(disposeTask);

            disposing = true;
            lifetimeCancellation.Cancel();
            disposeTask = DisposeCoreAsync(GetRefreshesDrainedTaskLocked());
            return new ValueTask(disposeTask);
        }
    }

    private async Task DisposeCoreAsync(Task refreshesDrainedTask)
    {
        await refreshesDrainedTask.ConfigureAwait(false);

        if (catalogService is not null)
            catalogService.PublicationChanged -= PublishStatus;
        foreach (var pair in firstFetches.ToArray())
            CompleteAndRemove(pair.Key, pair.Value, HostCatalogState.Unavailable);

        refreshGate.Dispose();
        lifetimeCancellation.Dispose();
    }

    private void BeginRefreshLocked() => activeRefreshes++;

    private void CompleteRefresh()
    {
        lock (lifecycleGate)
        {
            activeRefreshes--;
            if (activeRefreshes == 0)
                refreshesDrained?.TrySetResult(true);
        }
    }

    private Task GetRefreshesDrainedTaskLocked()
    {
        if (activeRefreshes == 0)
            return Task.CompletedTask;

        return (refreshesDrained ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
    }

    private void ObserveConnectedSessions()
    {
        if (instanceManager is null)
            return;

        var connected = instanceManager.Sessions.Select(IdentityOf).ToHashSet();
        foreach (var identity in connected)
            firstFetches.GetOrAdd(identity, static _ => CreateFirstFetch());

        foreach (var pair in firstFetches.ToArray())
        {
            if (!connected.Contains(pair.Key))
                CompleteAndRemove(pair.Key, pair.Value, HostCatalogState.Unavailable);
        }
    }

    private void CompleteAndRemove(
        HostCatalogIdentity identity,
        TaskCompletionSource<HostCatalogState> firstFetch,
        HostCatalogState state)
    {
        firstFetch.TrySetResult(state);
        ((ICollection<KeyValuePair<HostCatalogIdentity, TaskCompletionSource<HostCatalogState>>>)firstFetches)
            .Remove(new KeyValuePair<HostCatalogIdentity, TaskCompletionSource<HostCatalogState>>(identity, firstFetch));
    }

    private static HostCatalogIdentity IdentityOf(IHostMcpSession session) =>
        new(session.Instance.PipeName, session.Generation);

    private static TaskCompletionSource<HostCatalogState> CreateFirstFetch() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
