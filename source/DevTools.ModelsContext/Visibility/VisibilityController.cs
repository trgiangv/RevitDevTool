using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;

namespace DevTools.ModelsContext.Visibility;

public sealed class VisibilityController
{
    public const string NoActiveView = "There is no active view.";

    private readonly PlanExecutor _executor;
    private readonly Dictionary<(DocumentKey Model, string ViewId), ViewLedger> _ledgers = [];
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

    public JournalState Journal => View is null ? JournalState.Empty : _runtime.Journal.Get(View.Model, View.ViewId);

    public int PendingLinkChanges => Journal.LinkEntries.Count;

    private ViewLedger? Current => View is { } view && _ledgers.TryGetValue((view.Model, view.ViewId), out var ledger)
        ? ledger
        : null;

    public VisibilityPlan Plan(VisibilityRequest request)
    {
        return _runtime.Planner.Plan(request);
    }

    public VisibilityCheck Check(VisibilityRequest request)
    {
        return _runtime.Planner.Check(request);
    }

    public bool IsGroupHidden(long groupId)
    {
        return Current?.Groups.ContainsKey(groupId) == true;
    }

    public RowOverride? OverrideOf(ElementRef element)
    {
        return Current is { } ledger && ledger.Marks.TryGetValue(element, out var mark) ? mark : null;
    }

    public IEnumerable<KeyValuePair<long, RowOverride>> MarksOf(DocumentKey document)
    {
        return Current is { Marks.Count: > 0 } ledger
            ? ledger.Marks.Where(pair => pair.Key.Document == document)
                .Select(pair => new KeyValuePair<long, RowOverride>(pair.Key.Id, pair.Value))
            : [];
    }

    public RowOverride? CategoryOverrideOf(long groupId)
    {
        return Current is { } ledger && ledger.Categories.TryGetValue(groupId, out var category) ? category : null;
    }

    public bool IsHidden(ElementRef element)
    {
        if (element.Document.IsCurrentModel)
        {
            return _readBacks.HiddenOf(View, element.Document) is { } hidden && hidden.Contains(element.Id);
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

        var hidden = new HashSet<long>();
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
            return NoActiveView;
        }

        var plan = Plan(request);
        if (plan.IsDisabled)
        {
            return plan.DisabledReason;
        }

        var timer = TraceTimer.Start(_runtime.Logger);
        if (await _executor.ApplyAsync(view, LedgerOf(view), plan, CancellationToken.None) is { } reason)
        {
            return reason;
        }

        _readBacks.Touch(view, plan);
        _readBacks.Drop(view);
        await ReadBackAsync(CancellationToken.None);
        Changed?.Invoke(this, EventArgs.Empty);
        if (timer.IsEnabled)
        {
            _runtime.Logger.VisibilityApplied(plan.Action, plan.Steps.Count, ElementCountOf(plan), timer.ElapsedMs);
        }

        return _executor.TakeNotice();
    }

    public async Task KeepAsync(CancellationToken cancellationToken)
    {
        if (_runtime.IsBusy)
        {
            return;
        }

        await _runtime.Services.ViewActions.KeepAsync(cancellationToken);
        if (View is { } view)
        {
            _runtime.Journal.Reset(view.Model, view.ViewId);
        }

        await ClearAsync(cancellationToken);
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        if (_runtime.IsBusy)
        {
            return;
        }

        await _runtime.Services.ViewActions.ResetAsync(cancellationToken);
        var reason = View is { } view ? await _executor.RestoreLinksAsync(view, cancellationToken) : null;
        await ClearAsync(cancellationToken);
        if (reason is not null)
        {
            throw new InvalidOperationException(reason);
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

    private static int ElementCountOf(VisibilityPlan plan)
    {
        var count = 0;
        foreach (var step in plan.Steps)
        {
            count += step switch
            {
                ElementsStep elements => elements.Ids.Count,
                ReferenceCommandStep references => references.Ids.Count,
                LinkIsolateStep isolate => isolate.KeepIds.Count,
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

    private async Task ClearAsync(CancellationToken cancellationToken)
    {
        if (View is { } view)
        {
            var remaining = _runtime.Journal.Get(view.Model, view.ViewId).LinkEntries;
            _ledgers.Remove((view.Model, view.ViewId));
            if (remaining.Count > 0)
            {
                LedgerOf(view).Elements.UnionWith(remaining.Select(entry => entry.Element));
            }

            _readBacks.Drop(view);
            await ReadBackAsync(cancellationToken);
        }

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
