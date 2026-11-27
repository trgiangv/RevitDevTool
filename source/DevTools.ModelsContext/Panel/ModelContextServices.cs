using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Selection;
using DevTools.ModelsContext.Visibility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.ModelsContext.Panel;

public sealed class ModelContextServices(
    IModelSource source,
    IModelEvents events,
    ISelectionPort selection,
    IViewActions viewActions,
    IVisibilityExplainer explainer,
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

    public IVisibilityExplainer Explainer { get; } = explainer;

    public IFilterEvaluator Evaluator { get; } = evaluator;

    public IHostFilterCatalog HostFilters { get; } = hostFilters;

    public ICustomFilterStore CustomFilters { get; } = customFilters;

    public IClock Clock { get; } = clock;

    public IScheduler Scheduler { get; } = scheduler;

    public ILogger Logger { get; } = logger ?? NullLogger.Instance;
}
