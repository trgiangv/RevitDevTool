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

    public bool IsCategoryHidden(long categoryId)
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
               && ledger.IsHidden(element, snapshot.Groups[row.GroupIndex].GroupId);
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
        foreach (var row in snapshot.Rows)
        {
            if (ledger.IsHidden(new ElementRef(document.Key, row.Id), snapshot.Groups[row.GroupIndex].GroupId))
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

        var timer = TraceTimer.Start(_runtime.Logger);
        var before = await InViewBeforeAsync(plan);
        var (current, reason) = await OnActiveViewAsync(view, async target =>
            (target, await _executor.ApplyAsync(target, LedgerOf(target), plan, CancellationToken.None)));
        if (reason is not null)
        {
            return reason;
        }

        view = current;
        RecordCategoryOff(LedgerOf(view), before);

        _readBacks.Touch(view, plan);
        _readBacks.Drop(view);
        await ReadBackAsync(CancellationToken.None);
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

        _readBacks.Drop(view);
        await ReadBackAsync(cancellationToken);
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

        view = await OnActiveViewAsync(view, async target =>
        {
            await _runtime.Services.ViewActions.ResetAsync(target.ViewId, cancellationToken);
            return target;
        });
        var reason = await _executor.RestoreLinksAsync(view, cancellationToken);
        await ClearAsync(view, cancellationToken);
        if (reason is not null)
        {
            throw new InvalidOperationException(reason);
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

    internal HashSet<long> HiddenInViewIds(DocumentKey document)
    {
        var ids = View is { } view && !document.IsCurrentModel
            ? _runtime.Journal.LinkHideIds(view.Model, view.ViewId, document)
            : [];
        if (Current is { } ledger
            && ledger.CategoryOffIds.TryGetValue(document, out var off)
            && _runtime.Store.TryGet(document) is { } snapshot)
        {
            ids.UnionWith(off.Where(id => snapshot.TryGetRow(id, out var row)
                                          && ledger.IsCategoryOff(snapshot.Groups[row.GroupIndex].GroupId)));
        }

        return ids;
    }

    private async Task<List<(DocumentScope Document, IReadOnlyCollection<long> Ids)>> InViewBeforeAsync(VisibilityPlan plan)
    {
        var before = new List<(DocumentScope, IReadOnlyCollection<long>)>();
        if (plan.Action is not (VisibilityAction.Hide or VisibilityAction.Isolate) || !plan.Steps.Any(step => step is CategoryStep))
        {
            return before;
        }

        foreach (var document in _runtime.Data.Documents.Where(document => document.Info.IsLoaded))
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

    private static void RecordCategoryOff(
        ViewLedger ledger,
        List<(DocumentScope Document, IReadOnlyCollection<long> Ids)> before)
    {
        foreach (var (document, ids) in before)
        {
            var snapshot = document.Snapshot;
            ledger.AddCategoryOffIds(document.Key, ids.Where(id =>
                snapshot.TryGetRow(id, out var row) && ledger.IsCategoryOff(snapshot.Groups[row.GroupIndex].GroupId)));
        }
    }

    public bool HidByPanel(ElementRef element)
    {
        if (View is { } view && _runtime.Journal.HasLinkHide(view.Model, view.ViewId, element))
        {
            return true;
        }

        if (Current is not { } ledger)
        {
            return false;
        }

        if (ledger.HiddenElements.Contains(element))
        {
            return true;
        }

        return ledger.Isolation is { } isolation
               && _runtime.Store.TryGet(element.Document) is { } snapshot
               && snapshot.TryGetRow(element.Id, out var row)
               && !isolation.Keeps(element, snapshot.Groups[row.GroupIndex].GroupId);
    }

    internal void DropLinkFilters(DocumentKey model)
    {
        var dropped = false;
        foreach (var pair in _ledgers.Where(pair => pair.Key.Model.ModelId == model.ModelId))
        {
            dropped |= pair.Value.DropLinkFilters();
        }

        if (dropped)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    internal async Task RefreshHiddenAsync(CancellationToken cancellationToken)
    {
        if (await ReadBackAsync(cancellationToken))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    internal Task<bool> ReadBackAsync(CancellationToken cancellationToken)
    {
        return View is { } view ? _readBacks.ReadAsync(view, cancellationToken) : Task.FromResult(false);
    }

    internal void DropReadBack(SourceId source)
    {
        _readBacks.DropSource(source);
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

    private async Task ClearAsync(ViewInfo view, CancellationToken cancellationToken)
    {
        _ledgers.Remove((view.Model, view.ViewId));
        _notDrawn.Remove((view.Model, view.ViewId));
        _readBacks.Drop(view);
        await ReadBackAsync(cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
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
