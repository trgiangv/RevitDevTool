using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Selection;
using DevTools.ModelsContext.Visibility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;

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

internal sealed class PanelRuntime
{
    private readonly Dictionary<SourceId, Task<IReadOnlyList<ParameterInfo>>> _parameters = [];
    private int _generation;
    private int _running;
    private int _scheduled;

    public PanelRuntime(ModelContextServices services)
    {
        Services = services;
        Scheduler = new TrackedScheduler(services.Scheduler, this);
        Store = new SnapshotStore(services.Source, services.Logger);
        Guard = new SelectionGuard(services.Clock);
        Values = new ValueIndexCache(services.Evaluator, logger: services.Logger);
        Planner = new VisibilityPlanner(Store.Find, key => Data.Find(key));
    }

    public ContextData Data { get; set; } = ContextData.Empty;

    public ModelContextServices Services { get; }

    public ILogger Logger => Services.Logger;

    public SnapshotStore Store { get; }

    public ChangeJournal Journal { get; } = new();

    public SelectionGuard Guard { get; }

    public ValueIndexCache Values { get; }

    public VisibilityPlanner Planner { get; }

    public IScheduler Scheduler { get; }

    public bool IsBusy { get; set; }

    public string BusyText => VisibilityText.HostBusy(Services.Source.HostName);

    public PanelActivity Activity => new(Volatile.Read(ref _running) == 0 && Volatile.Read(ref _scheduled) == 0,
        Volatile.Read(ref _generation));

    public event EventHandler<string>? Failed;

    public event EventHandler? ActivityChanged;

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
        Logger.CatalogRead(source.Label, catalog.Count, timer.ElapsedMs);
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

    public Task RunAsync(Func<Task> work)
    {
        if (SynchronizationContext.Current is not null || Services.Scheduler.Context is not { } context)
        {
            return RunJobAsync(work);
        }

        var done = new TaskCompletionSource<bool>();
        context.Post(_ => RunJobAsync(work).ContinueWith(_ => done.SetResult(true), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default), null);
        return done.Task;
    }

    private async Task RunJobAsync(Func<Task> work)
    {
        Interlocked.Increment(ref _running);
        OnActivityChanged();
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
        finally
        {
            Interlocked.Increment(ref _generation);
            Interlocked.Decrement(ref _running);
            OnActivityChanged();
        }
    }

    private void Scheduled(int delta)
    {
        Interlocked.Add(ref _scheduled, delta);
        OnActivityChanged();
    }

    private void OnActivityChanged()
    {
        ActivityChanged?.Invoke(this, EventArgs.Empty);
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

    private sealed class TrackedScheduler(IScheduler inner, PanelRuntime owner) : IScheduler
    {
        public SynchronizationContext Context => inner.Context;

        public IDisposable Schedule(TimeSpan delay, Action action)
        {
            owner.Scheduled(1);
            var entry = new Entry(owner);
            entry.Inner = inner.Schedule(delay, () =>
            {
                if (!entry.TryClose())
                {
                    return;
                }

                try
                {
                    action();
                }
                finally
                {
                    owner.Scheduled(-1);
                }
            });
            return entry;
        }
    }

    private sealed class Entry(PanelRuntime owner) : IDisposable
    {
        private int _closed;

        public IDisposable? Inner { get; set; }

        public bool TryClose()
        {
            return Interlocked.Exchange(ref _closed, 1) == 0;
        }

        public void Dispose()
        {
            if (TryClose())
            {
                Inner?.Dispose();
                owner.Scheduled(-1);
            }
        }
    }
}

public readonly record struct PanelActivity(bool IsIdle, int Generation)
{
    public override string ToString()
    {
        return (IsIdle ? "Idle " : "Busy ") + Generation.ToString(CultureInfo.InvariantCulture);
    }
}
