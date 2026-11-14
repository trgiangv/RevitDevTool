using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.ViewModels;

public sealed class VisibilityController
{
    public const string NoActiveView = "There is no active view.";

    private readonly Dictionary<(DocumentKey Model, string ViewId), Ledger> _ledgers = [];
    private readonly Dictionary<(DocumentKey Model, string ViewId), ReadBack> _readBacks = [];
    private readonly PanelRuntime _runtime;
    private readonly Dictionary<(DocumentKey Model, string ViewId), HashSet<long>> _touched = [];
    private ViewInfo? _view;

    internal VisibilityController(PanelRuntime runtime)
    {
        _runtime = runtime;
    }

    public event EventHandler? Changed;

    public ViewInfo? View
    {
        get => _view;
        internal set
        {
            if (_view is not null && (value is null || KeyOf(value) != KeyOf(_view)))
            {
                _readBacks.Clear();
            }

            _view = value;
        }
    }

    internal Func<DocumentKey, IEnumerable<long>>? VisibleIds { get; set; }

    public JournalState Journal => View is null ? JournalState.Empty : _runtime.Journal.Get(View.Model, View.ViewId);

    public int PendingLinkChanges => Journal.LinkEntries.Count(entry =>
        entry.Action is VisibilityAction.Hide or VisibilityAction.Isolate);

    private Ledger? Current => View is { } view && _ledgers.TryGetValue((view.Model, view.ViewId), out var ledger)
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

    public RowOverride? OverrideOf(TreeRow row)
    {
        if (Current is not { HasMarks: true } ledger)
        {
            return null;
        }

        if (row.Kind == TreeRowKind.AllModelsGroup)
        {
            return CategoryOverrideOf(row.Id);
        }

        return ledger.Rows.TryGetValue(row.Key, out var mark) ? mark.Override : null;
    }

    public RowOverride? CategoryOverrideOf(long groupId)
    {
        return Current is { } ledger && ledger.Categories.TryGetValue(groupId, out var category) ? category : null;
    }

    public bool IsHidden(ElementRef element, long groupId)
    {
        if (element.Document.IsCurrentModel)
        {
            return ReadBackOf(element.Document) is { } readBack && readBack.Hidden.Contains(element.Id);
        }

        return Current is { } ledger && ledger.IsHidden(element, groupId);
    }

    public HashSet<long> HiddenIds(DocumentScope document)
    {
        if (document.Key.IsCurrentModel)
        {
            return ReadBackOf(document.Key) is { } readBack ? [.. readBack.Hidden] : [];
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

    public async Task<string?> RunAsync(VisibilityRequest request, CancellationToken cancellationToken)
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
        var actions = _runtime.Services.ViewActions;
        var ledger = LedgerOf(view);
        switch (plan.Action)
        {
            case VisibilityAction.Show:
                ledger.Remove(plan);
                ledger.Isolation = null;
                await actions.ResetAsync(cancellationToken);
                _runtime.Journal.Reset(view.Model, view.ViewId);
                await ReapplyAsync(view, ledger, cancellationToken);
                break;
            case VisibilityAction.Hide:
                await actions.ExecuteAsync(plan, cancellationToken);
                ledger.Add(plan);
                await RecordAsync(view, plan, cancellationToken);
                break;
            case VisibilityAction.Isolate:
                await actions.ExecuteAsync(plan, cancellationToken);
                ledger.Isolation = Isolation.From(plan);
                await RecordAsync(view, plan, cancellationToken);
                break;
            default:
                await actions.ExecuteAsync(plan, cancellationToken);
                ledger.Appearance.Add(plan);
                ledger.Mark(request.Rows, plan);
                await RecordAsync(view, plan, cancellationToken);
                break;
        }

        Touch(view, plan);
        _readBacks.Remove(KeyOf(view));
        await ReadBackAsync(view.Model.CurrentModel, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
        if (timer.IsEnabled)
        {
            _runtime.Logger.VisibilityApplied(plan.Action, plan.Steps.Count, ElementCountOf(plan), timer.ElapsedMs);
        }

        return null;
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
                _ => 0
            };
        }

        return count;
    }

    public async Task KeepAsync(CancellationToken cancellationToken)
    {
        if (_runtime.IsBusy)
        {
            return;
        }

        await _runtime.Services.ViewActions.KeepAsync(cancellationToken);
        await ClearAsync(cancellationToken);
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        if (_runtime.IsBusy)
        {
            return;
        }

        await _runtime.Services.ViewActions.ResetAsync(cancellationToken);
        await ClearAsync(cancellationToken);
    }

    internal async Task<bool> ReadBackAsync(DocumentKey document, CancellationToken cancellationToken)
    {
        if (!document.IsCurrentModel || View is not { } view || view.Model.CurrentModel != document)
        {
            return false;
        }

        var key = KeyOf(view);
        if (!_readBacks.TryGetValue(key, out var readBack))
        {
            readBack = new ReadBack();
            _readBacks.Add(key, readBack);
        }

        var candidates = new HashSet<long>(VisibleIds?.Invoke(document) ?? []);
        if (_touched.TryGetValue(key, out var touched))
        {
            candidates.UnionWith(touched);
        }

        candidates.ExceptWith(readBack.Checked);
        if (candidates.Count == 0)
        {
            return false;
        }

        var hidden = await _runtime.Services.ViewActions.GetIdsHiddenPermanentlyOrTemporarilyAsync(document,
            candidates, cancellationToken);
        readBack.Checked.UnionWith(candidates);
        var before = readBack.Hidden.Count;
        readBack.Hidden.UnionWith(hidden);
        return readBack.Hidden.Count != before;
    }

    internal void DropReadBack(SourceId source)
    {
        foreach (var key in _readBacks.Keys
                     .Where(key => _runtime.Store.Find(key.Model) is { } info && info.Source == source)
                     .ToList())
        {
            _readBacks.Remove(key);
        }
    }

    internal void Forget(DocumentKey document)
    {
        if (document.IsCurrentModel)
        {
            _runtime.Journal.ClearModel(document);
            foreach (var key in _ledgers.Keys.Where(key => key.Model.ModelId == document.ModelId).ToList())
            {
                _ledgers.Remove(key);
            }

            foreach (var key in _readBacks.Keys.Where(key => key.Model.ModelId == document.ModelId).ToList())
            {
                _readBacks.Remove(key);
            }

            foreach (var key in _touched.Keys.Where(key => key.Model.ModelId == document.ModelId).ToList())
            {
                _touched.Remove(key);
            }
        }
        else
        {
            _runtime.Journal.ClearLink(document);
            foreach (var ledger in _ledgers.Values)
            {
                ledger.Forget(document);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task ClearAsync(CancellationToken cancellationToken)
    {
        if (View is { } view)
        {
            _runtime.Journal.Reset(view.Model, view.ViewId);
            _ledgers.Remove((view.Model, view.ViewId));
            _readBacks.Remove(KeyOf(view));
            await ReadBackAsync(view.Model.CurrentModel, cancellationToken);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static (DocumentKey Model, string ViewId) KeyOf(ViewInfo view)
    {
        return (view.Model.CurrentModel, view.ViewId);
    }

    private ReadBack? ReadBackOf(DocumentKey document)
    {
        return View is { } view && view.Model.CurrentModel == document
                                && _readBacks.TryGetValue(KeyOf(view), out var readBack)
            ? readBack
            : null;
    }

    private void Touch(ViewInfo view, VisibilityPlan plan)
    {
        var model = view.Model.CurrentModel;
        foreach (var step in plan.Steps.OfType<ElementsStep>().Where(step => step.Document == model))
        {
            if (!_touched.TryGetValue(KeyOf(view), out var touched))
            {
                touched = [];
                _touched.Add(KeyOf(view), touched);
            }

            touched.UnionWith(step.Ids);
        }
    }

    private Ledger LedgerOf(ViewInfo view)
    {
        if (!_ledgers.TryGetValue((view.Model, view.ViewId), out var ledger))
        {
            ledger = new Ledger();
            _ledgers.Add((view.Model, view.ViewId), ledger);
        }

        return ledger;
    }

    private async Task ReapplyAsync(ViewInfo view, Ledger ledger, CancellationToken cancellationToken)
    {
        var actions = _runtime.Services.ViewActions;
        if (ledger.HidePlan() is { } hide)
        {
            await actions.ExecuteAsync(hide, cancellationToken);
            await RecordAsync(view, hide, cancellationToken);
        }

        foreach (var plan in ledger.Appearance)
        {
            await actions.ExecuteAsync(plan, cancellationToken);
            await RecordAsync(view, plan, cancellationToken);
        }
    }

    private async Task RecordAsync(ViewInfo view, VisibilityPlan plan, CancellationToken cancellationToken)
    {
        List<JournalEntry>? entries = null;
        if (plan.Action is VisibilityAction.Hide or VisibilityAction.Isolate)
        {
            entries = [];
            foreach (var step in plan.Steps.OfType<ReferenceCommandStep>())
            {
                var uniqueIds = await _runtime.Services.Source.GetUniqueIdsAsync(step.Document, step.Ids,
                    cancellationToken);
                entries.AddRange(step.Ids.Select(id => new JournalEntry(new ElementRef(step.Document, id),
                    uniqueIds.TryGetValue(id, out var uniqueId) ? uniqueId : string.Empty, plan.Action)));
            }
        }

        _runtime.Journal.Record(view.Model, view.ViewId, entries);
    }

    private sealed record Isolation(HashSet<ElementRef> Elements, HashSet<long> Groups, HashSet<DocumentKey> Links)
    {
        public static Isolation From(VisibilityPlan plan)
        {
            var isolation = new Isolation([], [], []);
            foreach (var step in plan.Steps)
            {
                switch (step)
                {
                    case GroupEverywhereStep group:
                        isolation.Groups.Add(group.GroupId);
                        break;
                    case AttachedModelStep model:
                        isolation.Links.Add(model.Document);
                        break;
                    case ElementsStep elements:
                        isolation.Elements.UnionWith(elements.Ids.Select(id => new ElementRef(elements.Document, id)));
                        break;
                    case ReferenceCommandStep references:
                        isolation.Elements.UnionWith(references.Ids.Select(id =>
                            new ElementRef(references.Document, id)));
                        break;
                }
            }

            return isolation;
        }

        public bool Keeps(ElementRef element, long groupId)
        {
            return Elements.Contains(element) || Groups.Contains(groupId) || Links.Contains(element.Document);
        }
    }

    private readonly record struct RowMark(DocumentKey? Document, RowOverride Override);

    private sealed class ReadBack
    {
        public HashSet<long> Checked { get; } = [];

        public HashSet<long> Hidden { get; } = [];
    }

    private sealed class Ledger
    {
        public HashSet<ElementRef> Elements { get; } = [];

        public Dictionary<long, string> Groups { get; } = [];

        public HashSet<DocumentKey> Links { get; } = [];

        public List<VisibilityPlan> Appearance { get; } = [];

        public Dictionary<string, RowMark> Rows { get; } = new(StringComparer.Ordinal);

        public Dictionary<long, RowOverride> Categories { get; } = [];

        public bool HasMarks => Rows.Count > 0 || Categories.Count > 0;

        public Isolation? Isolation { get; set; }

        public bool IsEmpty => Elements.Count == 0 && Groups.Count == 0 && Links.Count == 0 && Isolation is null;

        public bool IsHidden(ElementRef element, long groupId)
        {
            if (Isolation is { } isolation && !isolation.Keeps(element, groupId))
            {
                return true;
            }

            return Elements.Contains(element) || Groups.ContainsKey(groupId) || Links.Contains(element.Document);
        }

        public void Add(VisibilityPlan plan)
        {
            foreach (var step in plan.Steps)
            {
                switch (step)
                {
                    case GroupEverywhereStep group:
                        Groups[group.GroupId] = group.GroupName;
                        break;
                    case AttachedModelStep model:
                        Links.Add(model.Document);
                        break;
                    case ElementsStep elements:
                        Elements.UnionWith(elements.Ids.Select(id => new ElementRef(elements.Document, id)));
                        break;
                    case ReferenceCommandStep references:
                        Elements.UnionWith(references.Ids.Select(id => new ElementRef(references.Document, id)));
                        break;
                }
            }
        }

        public void Remove(VisibilityPlan plan)
        {
            foreach (var step in plan.Steps)
            {
                switch (step)
                {
                    case GroupEverywhereStep group:
                        Groups.Remove(group.GroupId);
                        break;
                    case AttachedModelStep model:
                        Links.Remove(model.Document);
                        break;
                    case ElementsStep elements:
                        Elements.ExceptWith(elements.Ids.Select(id => new ElementRef(elements.Document, id)));
                        break;
                    case ReferenceCommandStep references:
                        Elements.ExceptWith(references.Ids.Select(id => new ElementRef(references.Document, id)));
                        break;
                }
            }
        }

        public void Mark(IEnumerable<TreeRow> rows, VisibilityPlan plan)
        {
            foreach (var row in rows)
            {
                if (row.Kind == TreeRowKind.AllModelsGroup)
                {
                    Categories.TryGetValue(row.Id, out var category);
                    Categories[row.Id] = category.With(plan);
                }
                else
                {
                    Rows.TryGetValue(row.Key, out var mark);
                    Rows[row.Key] = new RowMark(row.Document, mark.Override.With(plan));
                }
            }
        }

        public void Forget(DocumentKey link)
        {
            foreach (var key in Rows.Where(pair => pair.Value.Document == link).Select(pair => pair.Key).ToList())
            {
                Rows.Remove(key);
            }

            Links.Remove(link);
            Elements.RemoveWhere(element => element.Document == link);
            Appearance.RemoveAll(plan => plan.Steps.Any(step => step is AttachedModelStep model && model.Document == link));
        }

        public VisibilityPlan? HidePlan()
        {
            var steps = new List<VisibilityStep>();
            steps.AddRange(Groups.Select(pair => new GroupEverywhereStep(pair.Key, pair.Value)));
            steps.AddRange(Links.Select(link => new AttachedModelStep(link)));
            foreach (var group in Elements.GroupBy(element => element.Document).OrderBy(group => !group.Key.IsCurrentModel))
            {
                var ids = group.Select(element => element.Id).OrderBy(id => id).ToList();
                steps.Add(group.Key.IsCurrentModel
                    ? new ElementsStep(group.Key, ids)
                    : new ReferenceCommandStep(group.Key, ids));
            }

            return steps.Count == 0 ? null : new VisibilityPlan(VisibilityAction.Hide, null, steps, null);
        }
    }
}
