using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Panel;

internal sealed class HostEventRelay : IDisposable
{
    private readonly List<ElementDelta> _deltas = [];
    private readonly Debouncer _deltaDebouncer;
    private readonly Debouncer _documentsDebouncer;
    private readonly object _gate = new();
    private readonly ModelContextViewModel _owner;
    private readonly PanelRuntime _runtime;
    private readonly SessionSwitcher _sessions;
    private readonly Debouncer _viewDebouncer;
    private readonly VisibilityController _visibility;
    private ViewInfo? _incomingView;

    public HostEventRelay(
        PanelRuntime runtime,
        VisibilityController visibility,
        SessionSwitcher sessions,
        ModelContextViewModel owner)
    {
        _runtime = runtime;
        _visibility = visibility;
        _sessions = sessions;
        _owner = owner;
        var scheduler = runtime.Scheduler;
        var delay = ModelContextViewModel.EventDelay;
        _deltaDebouncer = new Debouncer(scheduler, delay, () => _ = runtime.RunAsync(OnElementsSettledAsync));
        _documentsDebouncer = new Debouncer(scheduler, delay, () => _ = runtime.RunAsync(sessions.ReloadDocumentsAsync));
        _viewDebouncer = new Debouncer(scheduler, delay, () => _ = runtime.RunAsync(OnViewSettledAsync));
        var events = runtime.Services.Events;
        events.ElementsChanged += OnElementsChanged;
        events.DocumentsChanged += OnDocumentsChanged;
        events.ViewActivated += OnViewActivated;
        events.DocumentClosing += OnDocumentClosing;
        events.ActiveModelChanged += OnActiveModelChanged;
        events.ModelSynced += OnModelSynced;
        events.LinkStatusChanged += OnLinkStatusChanged;
        events.ParameterDefinitionsChanged += OnParameterDefinitionsChanged;
        events.ViewFiltersRemoved += OnViewFiltersRemoved;
        events.ModelRenamed += OnModelRenamed;
    }

    public void Dispose()
    {
        var events = _runtime.Services.Events;
        events.ElementsChanged -= OnElementsChanged;
        events.DocumentsChanged -= OnDocumentsChanged;
        events.ViewActivated -= OnViewActivated;
        events.DocumentClosing -= OnDocumentClosing;
        events.ActiveModelChanged -= OnActiveModelChanged;
        events.ModelSynced -= OnModelSynced;
        events.LinkStatusChanged -= OnLinkStatusChanged;
        events.ParameterDefinitionsChanged -= OnParameterDefinitionsChanged;
        events.ViewFiltersRemoved -= OnViewFiltersRemoved;
        events.ModelRenamed -= OnModelRenamed;
        _deltaDebouncer.Dispose();
        _documentsDebouncer.Dispose();
        _viewDebouncer.Dispose();
    }

    public void Pause()
    {
        _deltaDebouncer.Cancel();
        _documentsDebouncer.Cancel();
        _viewDebouncer.Cancel();
        _sessions.Active?.Cancel();
        List<ElementDelta> deltas;
        lock (_gate)
        {
            deltas = [.. _deltas];
            _deltas.Clear();
        }

        foreach (var delta in deltas)
        {
            MarkChanged(delta);
        }
    }

    public void ClearView()
    {
        lock (_gate)
        {
            _incomingView = null;
        }
    }

    private void MarkChanged(ElementDelta delta)
    {
        _runtime.Store.MarkDirty(delta.Source);
        Invalidate(delta);
    }

    private void Invalidate(ElementDelta delta)
    {
        _runtime.Values.Invalidate(delta.Source);
        _visibility.DropReadBack(delta.Source);
        if (delta.IsUndoRedo && _sessions.Active is { } session)
        {
            _runtime.Journal.MarkStale(session.Model);
        }
    }

    private void TriggerDocuments()
    {
        if (_owner.IsPaneVisible)
        {
            _documentsDebouncer.Trigger();
        }
    }

    private void OnElementsChanged(object? sender, ElementDelta delta)
    {
        if (!_owner.IsPaneVisible)
        {
            MarkChanged(delta);
            return;
        }

        lock (_gate)
        {
            _deltas.Add(delta);
        }

        _deltaDebouncer.Trigger();
    }

    private void OnDocumentsChanged(object? sender, EventArgs e)
    {
        TriggerDocuments();
    }

    private void OnViewActivated(object? sender, ViewInfo view)
    {
        lock (_gate)
        {
            _incomingView = view;
        }

        if (_owner.IsPaneVisible)
        {
            _viewDebouncer.Trigger();
        }
    }

    private void OnDocumentClosing(object? sender, DocumentKey document)
    {
        _sessions.Forget(document);
        TriggerDocuments();
    }

    private void OnActiveModelChanged(object? sender, DocumentKey model)
    {
        if (_owner.IsPaneVisible)
        {
            _ = _runtime.RunAsync(() => _sessions.SwitchAsync(model.CurrentModel, null));
        }
    }

    private void OnModelSynced(object? sender, DocumentKey model)
    {
        _ = _runtime.RunAsync(() => _sessions.VerifyAsync(model.CurrentModel));
    }

    private void OnLinkStatusChanged(object? sender, LinkStatusChange change)
    {
        if (change.Status == LinkStatus.Reloaded && _runtime.Store.Find(change.Link) is { } info)
        {
            _sessions.AddReloaded(info.Source);
        }

        TriggerDocuments();
    }

    private void OnParameterDefinitionsChanged(object? sender, SourceId source)
    {
        _runtime.ForgetParameters(source);
        if (_owner.IsPaneVisible)
        {
            _ = _runtime.RunAsync(() => _owner.Filter.RefreshParametersAsync(source));
        }
    }

    private void OnModelRenamed(object? sender, ModelRename rename)
    {
        lock (_gate)
        {
            if (_incomingView is { } view)
            {
                _incomingView = rename.Map(view);
            }

            for (var index = 0; index < _deltas.Count; index++)
            {
                _deltas[index] = _deltas[index] with { Source = rename.Map(_deltas[index].Source) };
            }
        }

        _owner.Rename(rename);
    }

    private void OnViewFiltersRemoved(object? sender, DocumentKey model)
    {
        _visibility.DropLinkFilters(model);
    }

    private async Task OnElementsSettledAsync()
    {
        List<ElementDelta> deltas;
        lock (_gate)
        {
            deltas = [.. _deltas];
            _deltas.Clear();
        }

        var timer = TraceTimer.Start(_runtime.Logger);
        foreach (var delta in deltas)
        {
            _runtime.Store.Apply(delta);
            Invalidate(delta);
        }

        if (timer.IsEnabled)
        {
            _runtime.Logger.DeltasApplied(deltas.Count, deltas.Sum(delta => delta.ChangedCount), timer.ElapsedMs);
        }

        if (_owner.ContextBar.Scope != ScopeKind.ActiveView
            && deltas.TrueForAll(delta => delta.ChangedCount == 0 && !delta.IsUndoRedo))
        {
            await _visibility.RefreshHiddenAsync(CancellationToken.None);
            return;
        }

        await _owner.RefreshAsync();
    }

    private async Task OnViewSettledAsync()
    {
        ViewInfo? view;
        lock (_gate)
        {
            view = _incomingView;
            _incomingView = null;
        }

        if (view is null)
        {
            return;
        }

        if (_sessions.Active is not { } session || session.Model != view.Model.CurrentModel)
        {
            await _sessions.SwitchAsync(view.Model.CurrentModel, null);
            return;
        }

        _owner.ShowView(view);
        var documents = _owner.ContextBar.Documents.Select(item => item.Info).ToList();
        var checks = await _sessions.ViewChecksAsync(documents, session.Token);
        if (!ReferenceEquals(session, _sessions.Active))
        {
            return;
        }

        _owner.SetDocumentsQuietly(documents, checks);
        await _owner.TemporaryBar.RefreshAsync(session.Token);
        await _owner.RefreshAsync();
    }
}
