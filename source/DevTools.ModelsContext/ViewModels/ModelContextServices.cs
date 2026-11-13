using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;

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
    IScheduler scheduler)
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
}

internal sealed class PanelRuntime
{
    public PanelRuntime(ModelContextServices services)
    {
        Services = services;
        Store = new SnapshotStore(services.Source);
        Guard = new SelectionGuard(services.Clock);
        Values = new ValueIndexCache(services.Evaluator);
        Planner = new VisibilityPlanner(Store.Find, Store.TryGet);
    }

    public ModelContextServices Services { get; }

    public SnapshotStore Store { get; }

    public TemporaryJournal Journal { get; } = new();

    public SelectionGuard Guard { get; }

    public ValueIndexCache Values { get; }

    public VisibilityPlanner Planner { get; }

    public event EventHandler<string>? Failed;

    public async Task RunAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Failed?.Invoke(this, exception.Message);
        }
    }

    public async Task SelectAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        Guard.RecordSent(elements);
        await Services.Selection.SelectAsync(elements, cancellationToken);
    }
}
