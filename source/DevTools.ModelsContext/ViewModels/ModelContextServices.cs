using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.ModelsContext.ViewModels;

public sealed class ModelContextServices(
    IModelSource source,
    IModelEvents events,
    ISelectionPort selection,
    IViewActions viewActions,
    IFilterEvaluator evaluator,
    IHostFilterCatalog hostFilters,
    ICustomFilterStore customFilters,
    IClock clock,
    IScheduler scheduler,
    ILogger? logger = null)
{
    public IModelSource Source { get; } = source;

    public IModelEvents Events { get; } = events;

    public ISelectionPort Selection { get; } = selection;

    public IViewActions ViewActions { get; } = viewActions;

    public IFilterEvaluator Evaluator { get; } = evaluator;

    public IHostFilterCatalog HostFilters { get; } = hostFilters;

    public ICustomFilterStore CustomFilters { get; } = customFilters;

    public IClock Clock { get; } = clock;

    public IScheduler Scheduler { get; } = scheduler;

    public ILogger Logger { get; } = logger ?? NullLogger.Instance;
}

internal sealed class PanelRuntime
{
    private readonly Dictionary<SourceId, Task<IReadOnlyList<ParameterInfo>>> _parameters = [];

    public PanelRuntime(ModelContextServices services)
    {
        Services = services;
        Store = new SnapshotStore(services.Source, services.Logger);
        Guard = new SelectionGuard(services.Clock);
        Values = new ValueIndexCache(services.Evaluator, logger: services.Logger);
        Planner = new VisibilityPlanner(Store.Find, Store.TryGet);
    }

    public ModelContextServices Services { get; }

    public ILogger Logger => Services.Logger;

    public SnapshotStore Store { get; }

    public TemporaryJournal Journal { get; } = new();

    public SelectionGuard Guard { get; }

    public ValueIndexCache Values { get; }

    public VisibilityPlanner Planner { get; }

    public bool IsBusy { get; set; }

    public string BusyText => VisibilityReasons.HostBusy(Services.Source.HostName);

    public event EventHandler<string>? Failed;

    public Task<IReadOnlyList<ParameterInfo>> ParametersAsync(SourceId source, CancellationToken cancellationToken)
    {
        lock (_parameters)
        {
            if (_parameters.TryGetValue(source, out var task) && !task.IsFaulted && !task.IsCanceled)
            {
                return task;
            }

            task = Logger.IsEnabled(LogLevel.Trace)
                ? TimedCatalogAsync(source, cancellationToken)
                : Services.Source.GetParameterCatalogAsync(source, cancellationToken);
            _parameters[source] = task;
            return task;
        }
    }

    private async Task<IReadOnlyList<ParameterInfo>> TimedCatalogAsync(SourceId source, CancellationToken cancellationToken)
    {
        var timer = TraceTimer.Start(Logger);
        var catalog = await Services.Source.GetParameterCatalogAsync(source, cancellationToken).ConfigureAwait(false);
        Logger.CatalogRead(source, catalog.Count, timer.ElapsedMs);
        return catalog;
    }

    public void ForgetParameters(SourceId source)
    {
        lock (_parameters)
        {
            _parameters.Remove(source);
        }
    }

    public void Forget(IEnumerable<SourceId> sources)
    {
        foreach (var source in sources)
        {
            Values.Invalidate(source);
            lock (_parameters)
            {
                _parameters.Remove(source);
            }
        }
    }

    public async Task RunAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (OperationCanceledException)
        {
            Logger.Canceled();
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    public bool TryRun(Action work)
    {
        try
        {
            work();
            return true;
        }
        catch (Exception exception)
        {
            Fail(exception);
            return false;
        }
    }

    private void Fail(Exception exception)
    {
        Logger.Failed(exception, exception.Message);
        Failed?.Invoke(this, exception.Message);
    }

    public async Task SelectAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        Guard.RecordSent(elements);
        var timer = TraceTimer.Start(Logger);
        await Services.Selection.SelectAsync(elements, cancellationToken);
        if (timer.IsEnabled)
        {
            Logger.SelectionSent(elements.Count, timer.ElapsedMs);
        }
    }
}
