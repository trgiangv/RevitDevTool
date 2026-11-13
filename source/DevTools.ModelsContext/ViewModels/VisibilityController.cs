using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.ViewModels;

public sealed class VisibilityController
{
    public const string NoActiveView = "There is no active view.";

    private readonly Dictionary<(DocumentKey Model, string ViewId), Ledger> _ledgers = [];
    private readonly PanelRuntime _runtime;

    internal VisibilityController(PanelRuntime runtime)
    {
        _runtime = runtime;
    }

    public event EventHandler? Changed;

    public ViewInfo? View { get; internal set; }

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

    public bool IsGroupHidden(long groupId)
    {
        return Current?.Groups.ContainsKey(groupId) == true;
    }

    public bool IsHidden(ElementRef element, long groupId)
    {
        return Current is { } ledger && ledger.IsHidden(element, groupId);
    }

    public HashSet<long> HiddenIds(DocumentScope document)
    {
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
        if (View is not { } view)
        {
            return NoActiveView;
        }

        var plan = Plan(request);
        if (plan.IsDisabled)
        {
            return plan.DisabledReason;
        }

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
                Record(view, plan);
                break;
            case VisibilityAction.Isolate:
                await actions.ExecuteAsync(plan, cancellationToken);
                ledger.Isolation = Isolation.From(plan);
                Record(view, plan);
                break;
            default:
                await actions.ExecuteAsync(plan, cancellationToken);
                ledger.Appearance.Add(plan);
                Record(view, plan);
                break;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return null;
    }

    public async Task KeepAsync(CancellationToken cancellationToken)
    {
        await _runtime.Services.ViewActions.KeepAsync(cancellationToken);
        Clear();
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await _runtime.Services.ViewActions.ResetAsync(cancellationToken);
        Clear();
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

    private void Clear()
    {
        if (View is { } view)
        {
            _runtime.Journal.Reset(view.Model, view.ViewId);
            _ledgers.Remove((view.Model, view.ViewId));
        }

        Changed?.Invoke(this, EventArgs.Empty);
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
            Record(view, hide);
        }

        foreach (var plan in ledger.Appearance)
        {
            await actions.ExecuteAsync(plan, cancellationToken);
            Record(view, plan);
        }
    }

    private void Record(ViewInfo view, VisibilityPlan plan)
    {
        var entries = plan.Action is VisibilityAction.Hide or VisibilityAction.Isolate
            ? plan.Steps.OfType<ReferenceCommandStep>()
                .SelectMany(step => step.Ids.Select(id =>
                    new JournalEntry(new ElementRef(step.Document, id), string.Empty, plan.Action)))
                .ToList()
            : null;
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

    private sealed class Ledger
    {
        public HashSet<ElementRef> Elements { get; } = [];

        public Dictionary<long, string> Groups { get; } = [];

        public HashSet<DocumentKey> Links { get; } = [];

        public List<VisibilityPlan> Appearance { get; } = [];

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

        public void Forget(DocumentKey link)
        {
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
