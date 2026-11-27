using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;

namespace DevTools.ModelsContext.Visibility;

public sealed class VisibilityController
{
    private readonly PlanExecutor _executor;
    private readonly Dictionary<(DocumentKey Model, string ViewId), ViewLedger> _ledgers = [];
    private readonly Dictionary<(DocumentKey Model, string ViewId), HashSet<ElementRef>> _notDrawn = [];
    private readonly ReadBackCache _readBacks;
    private readonly PanelRuntime _runtime;
    private ViewInfo? _view;

    internal VisibilityController(PanelRuntime runtime)
    {
        _runtime = runtime;
        _executor = new PlanExecutor(runtime);
        _readBacks = new ReadBackCache(runtime);
    }

    public event EventHandler? Changed;

    internal event EventHandler? RefreshNeeded;

    public ViewInfo? View
    {
        get => _view;
        internal set
        {
            if (_view is not null && (value is null || ReadBackCache.KeyOf(value) != ReadBackCache.KeyOf(_view)))
            {
                _readBacks.Clear();
            }

            _view = value;
        }
    }

    public JournalPage Journal => View is null ? JournalPage.Empty : _runtime.Journal.Get(View.Model, View.ViewId);

    public int LinkHideCount => Journal.LinkHides.Count;

    private ViewLedger? Current => View is { } view && _ledgers.TryGetValue((view.Model, view.ViewId), out var ledger)
        ? ledger
        : null;

    public ActionState Check(VisibilityRequest request)
    {
        return _runtime.Planner.Check(request);
    }

    public bool HidesCategory(long categoryId)
    {
        return Current?.HiddenCategories.ContainsKey(categoryId) == true;
    }

    public RowOverride? OverrideOf(ElementRef element)
    {
        return Current is { } ledger && ledger.Overrides.TryGetValue(element, out var value) ? value : null;
    }

    public IEnumerable<KeyValuePair<long, RowOverride>> OverridesOf(DocumentKey document)
    {
        return Current is { Overrides.Count: > 0 } ledger
            ? ledger.Overrides.Where(pair => pair.Key.Document == document)
                .Select(pair => new KeyValuePair<long, RowOverride>(pair.Key.Id, pair.Value))
            : [];
    }

    public RowOverride? CategoryOverrideOf(long categoryId)
    {
        return Current is { } ledger && ledger.CategoryOverrides.TryGetValue(categoryId, out var value) ? value : null;
    }

    public bool IsHidden(ElementRef element)
    {
        if (element.Document.IsCurrentModel)
        {
            return _readBacks.HiddenOf(View, element.Document) is { } hidden && hidden.Contains(element.Id);
        }

        if (View is { } view && _runtime.Journal.HasLinkHide(view.Model, view.ViewId, element))
        {
            return true;
        }

        return Current is { IsEmpty: false } ledger
               && _runtime.Store.TryGet(element.Document) is { } snapshot
               && snapshot.TryGetRow(element.Id, out var row)
               && ledger.Hides(element, snapshot.Groups[row.GroupIndex].GroupId);
    }

    internal void Watch(Func<IEnumerable<ElementRef>> elements)
    {
        _readBacks.Watch(elements);
    }

    public HashSet<long> HiddenIds(DocumentScope document)
    {
        if (document.Key.IsCurrentModel)
        {
            return _readBacks.HiddenOf(View, document.Key) is { } readBack ? [.. readBack] : [];
        }

        var hidden = View is { } view ? _runtime.Journal.LinkHideIds(view.Model, view.ViewId, document.Key) : [];
        if (Current is not { IsEmpty: false } ledger)
        {
            return hidden;
        }

        var snapshot = document.Snapshot;
        var undrawn = document.UndrawnIds(View);
        foreach (var row in snapshot.Rows)
        {
            if ((document.InScope(row.Id) || undrawn.Contains(row.Id))
                && ledger.Hides(new ElementRef(document.Key, row.Id), snapshot.Groups[row.GroupIndex].GroupId))
            {
                hidden.Add(row.Id);
            }
        }

        return hidden;
    }

    public async Task<string?> RunAsync(VisibilityRequest request)
    {
        if (_runtime.IsBusy)
        {
            return _runtime.BusyText;
        }

        if (View is not { } view)
        {
            return VisibilityText.NoActiveView;
        }

        var plan = _runtime.Planner.Plan(request);
        if (plan.IsDisabled)
        {
            return plan.DisabledReason;
        }

        return await ActAsync(() => ApplyAsync(view, plan));
    }

    private async Task<string?> ApplyAsync(ViewInfo view, VisibilityPlan plan)
    {
        var timer = TraceTimer.Start(_runtime.Logger);
        var before = await InViewBeforeAsync(plan);
        var (current, reason) = await OnActiveViewAsync(view, async target =>
        {
            _readBacks.Touch(target, plan);
            var request = _readBacks.RequestFor(target);
            return (target, await _executor.ApplyAsync(target, LedgerOf(target), plan, request, CancellationToken.None));
        });
        if (reason is not null)
        {
            return reason;
        }

        view = current;
        var ledger = LedgerOf(view);
        foreach (var (document, ids) in before)
        {
            ledger.CategoryTaken.Add(document.Key, ids.Where(id => CategoryOf(document.Snapshot, id) is { } category
                                                                  && ledger.HidesCategory(category)));
        }

        _readBacks.Store(view, _executor.TakeReadBack());
        await ReadBackAsync(false, CancellationToken.None);
        RecordTaken(view, before);
        RecordNotDrawn(view, plan);
        Changed?.Invoke(this, EventArgs.Empty);
        if (timer.IsEnabled)
        {
            _runtime.Logger.VisibilityApplied(plan.Action, plan.Steps.Count, ElementCountOf(plan), timer.ElapsedMs);
        }

        return _executor.TakeNotice();
    }

    public async Task KeepAsync(CancellationToken cancellationToken)
    {
        if (_runtime.IsBusy || View is not { } view)
        {
            return;
        }

        await ActAsync(() => KeepAsync(view, cancellationToken));
    }

    private async Task KeepAsync(ViewInfo view, CancellationToken cancellationToken)
    {
        view = await OnActiveViewAsync(view, async target =>
        {
            await _runtime.Services.ViewActions.KeepAsync(target.ViewId, cancellationToken);
            return target;
        });
        _runtime.Journal.Keep(view.Model, view.ViewId);
        if (_ledgers.TryGetValue((view.Model, view.ViewId), out var ledger))
        {
            ledger.Keep();
        }

        _readBacks.Forget(view);
        await ReadBackAsync(false, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void Rename(ModelRename rename)
    {
        foreach (var pair in _ledgers.Where(pair => pair.Key.Model.ModelId == rename.OldId).ToList())
        {
            _ledgers.Remove(pair.Key);
            pair.Value.Rename(rename);
            _ledgers[(rename.Map(pair.Key.Model), pair.Key.ViewId)] = pair.Value;
        }

        foreach (var pair in _notDrawn.Where(pair => pair.Key.Model.ModelId == rename.OldId).ToList())
        {
            _notDrawn.Remove(pair.Key);
            _notDrawn[(rename.Map(pair.Key.Model), pair.Key.ViewId)] = [.. pair.Value.Select(element => rename.Map(element))];
        }

        _readBacks.ForgetModel(DocumentKey.Current(rename.OldId));
        if (_view is { } view)
        {
            _view = rename.Map(view);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        if (_runtime.IsBusy || View is not { } view)
        {
            return;
        }

        await ActAsync(() => ResetAsync(view, cancellationToken));
    }

    private async Task ResetAsync(ViewInfo view, CancellationToken cancellationToken)
    {
        var request = _readBacks.RequestFor(view);
        var (current, result) = await OnActiveViewAsync(view, async target =>
            (target, await _runtime.Services.ViewActions.ResetAsync(target.ViewId, request, cancellationToken)));
        view = current;
        var reason = await _executor.RestoreLinksAsync(view, cancellationToken);
        await ClearAsync(view, result, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
        if (reason is not null)
        {
            throw new InvalidOperationException(reason);
        }
    }

    private async Task ActAsync(Func<Task> work)
    {
        await ActAsync(async () =>
        {
            await work();
            return true;
        });
    }

    private async Task<T> ActAsync<T>(Func<Task<T>> work)
    {
        _runtime.RunningActions.Begin();
        try
        {
            return await work();
        }
        finally
        {
            if (_runtime.RunningActions.End())
            {
                RefreshNeeded?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public HashSet<long> NotDrawnIds(DocumentKey document)
    {
        return View is { } view && _notDrawn.TryGetValue((view.Model, view.ViewId), out var elements)
            ? [.. elements.Where(element => element.Document == document).Select(element => element.Id)]
            : [];
    }

    private void RecordNotDrawn(ViewInfo view, VisibilityPlan plan)
    {
        if (plan.Action != VisibilityAction.Hide)
        {
            return;
        }

        var requested = plan.Steps.SelectMany(step => step switch
        {
            ElementsStep elements => elements.Ids.Select(id => new ElementRef(elements.Document, id)),
            LinkElementsStep linked => linked.Ids.Select(id => new ElementRef(linked.Document, id)),
            _ => []
        }).ToList();
        if (requested.Count == 0)
        {
            return;
        }

        if (!_notDrawn.TryGetValue((view.Model, view.ViewId), out var notDrawn))
        {
            notDrawn = [];
            _notDrawn.Add((view.Model, view.ViewId), notDrawn);
        }

        foreach (var element in requested)
        {
            if (IsHidden(element))
            {
                notDrawn.Remove(element);
            }
            else
            {
                notDrawn.Add(element);
            }
        }
    }

    internal HashSet<long> TakenIds(DocumentKey document)
    {
        var ids = View is { } view && !document.IsCurrentModel
            ? _runtime.Journal.LinkHideIds(view.Model, view.ViewId, document)
            : [];
        if (Current is { } ledger
            && ledger.CategoryTaken.TryGetValue(document, out var taken)
            && _runtime.Store.TryGet(document) is { } snapshot)
        {
            ids.UnionWith(taken.Where(id => CategoryOf(snapshot, id) is { } category && ledger.HidesCategory(category)));
        }

        if (Current is { } current && current.ElementTaken.TryGetValue(document, out var elements))
        {
            ids.UnionWith(elements);
        }

        return ids;
    }

    private void RecordTaken(
        ViewInfo view,
        List<(DocumentScope Document, IReadOnlyCollection<long> Ids)> before)
    {
        var ledger = LedgerOf(view);
        var model = view.Model.CurrentModel;
        if (_readBacks.HiddenOf(view, model) is not { } hidden || _readBacks.ReadOf(view, model) is not { } read)
        {
            return;
        }

        bool StillHidden(long id) => read.Contains(id)
            ? hidden.Contains(id)
            : Hides(new ElementRef(model, id));

        if (ledger.ElementTaken.TryGetValue(model, out var known))
        {
            known.RemoveWhere(id => !StillHidden(id));
        }

        ledger.ElementTaken.Add(model, before
            .Where(pair => pair.Document.Key == model)
            .SelectMany(pair => pair.Ids)
            .Where(StillHidden));
    }

    private async Task<List<(DocumentScope Document, IReadOnlyCollection<long> Ids)>> InViewBeforeAsync(VisibilityPlan plan)
    {
        var before = new List<(DocumentScope, IReadOnlyCollection<long>)>();
        if (plan.Action is not (VisibilityAction.Hide or VisibilityAction.Isolate))
        {
            return before;
        }

        var categories = plan.Steps.Any(step => step is CategoryStep);
        foreach (var document in _runtime.Data.Documents.Where(document =>
                     document.Info.IsLoaded && (categories || document.Key.IsCurrentModel)))
        {
            if (_runtime.Data.Scope == ScopeKind.ActiveView)
            {
                if (document.Ids is { } ids)
                {
                    before.Add((document, ids));
                }

                continue;
            }

            try
            {
                before.Add((document, await _runtime.Services.Source.CollectScopeAsync(document.Key, ScopeKind.ActiveView,
                    CancellationToken.None)));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _runtime.Logger.Swallowed(exception, "View ids before category action", document.Key.ToString());
            }
        }

        return before;
    }

    public bool Hides(ElementRef element)
    {
        if (View is { } view && _runtime.Journal.HasLinkHide(view.Model, view.ViewId, element))
        {
            return true;
        }

        if (Current is not { } ledger)
        {
            return false;
        }

        return ledger.HiddenElements.Contains(element)
               || (ledger.Isolation is { } isolation
                   && _runtime.Store.TryGet(element.Document) is { } snapshot
                   && CategoryOf(snapshot, element.Id) is { } category
                   && !isolation.Keeps(element, category));
    }

    private static long? CategoryOf(DocumentSnapshot snapshot, long id)
    {
        return snapshot.TryGetRow(id, out var row) ? snapshot.Groups[row.GroupIndex].GroupId : null;
    }

    internal void ForgetLinkFilters(DocumentKey model)
    {
        var forgot = false;
        foreach (var pair in _ledgers.Where(pair => pair.Key.Model.ModelId == model.ModelId))
        {
            forgot |= pair.Value.ForgetLinkFilters();
        }

        if (forgot)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    internal async Task<bool> ReadBackAsync(bool raiseChanged, CancellationToken cancellationToken)
    {
        var changed = View is { } view && await _readBacks.ReadBackAsync(view, cancellationToken);
        if (changed && raiseChanged)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    internal void ForgetReadBack(SourceId source)
    {
        _readBacks.ForgetSource(source);
    }

    internal void Forget(DocumentKey document)
    {
        if (document.IsCurrentModel)
        {
            ForgetModel(document);
        }
        else
        {
            ForgetLink(document);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task<T> OnActiveViewAsync<T>(ViewInfo view, Func<ViewInfo, Task<T>> work)
    {
        try
        {
            return await work(view);
        }
        catch (ViewChangedException)
        {
            var active = await _runtime.Services.ViewActions.ReadActiveViewAsync(CancellationToken.None);
            if (active?.View is not { } fresh || fresh.Model != view.Model || fresh.ViewId == view.ViewId)
            {
                throw;
            }

            View = fresh;
            return await work(fresh);
        }
    }

    private static int ElementCountOf(VisibilityPlan plan)
    {
        var count = 0;
        foreach (var step in plan.Steps)
        {
            count += step switch
            {
                ElementsStep elements => elements.Ids.Count,
                LinkElementsStep linked => linked.Ids.Count,
                LinkIsolateStep isolate => isolate.KeptIds.Count,
                LinkFilterStep filter => filter.Ids.Count,
                _ => 0
            };
        }

        return count;
    }

    private void ForgetModel(DocumentKey document)
    {
        _runtime.Journal.ClearModel(document);
        foreach (var key in _ledgers.Keys.Where(key => key.Model.ModelId == document.ModelId).ToList())
        {
            _ledgers.Remove(key);
        }

        foreach (var key in _notDrawn.Keys.Where(key => key.Model.ModelId == document.ModelId).ToList())
        {
            _notDrawn.Remove(key);
        }

        _readBacks.ForgetModel(document);
    }

    private void ForgetLink(DocumentKey document)
    {
        _runtime.Journal.ClearLink(document);
        foreach (var ledger in _ledgers.Values)
        {
            ledger.Forget(document);
        }
    }

    private async Task ClearAsync(ViewInfo view, ReadBackResult? result, CancellationToken cancellationToken)
    {
        _ledgers.Remove((view.Model, view.ViewId));
        _notDrawn.Remove((view.Model, view.ViewId));
        _readBacks.Store(view, result);
        await ReadBackAsync(false, cancellationToken);
    }

    private ViewLedger LedgerOf(ViewInfo view)
    {
        if (!_ledgers.TryGetValue((view.Model, view.ViewId), out var ledger))
        {
            ledger = new ViewLedger();
            _ledgers.Add((view.Model, view.ViewId), ledger);
        }

        return ledger;
    }
}
