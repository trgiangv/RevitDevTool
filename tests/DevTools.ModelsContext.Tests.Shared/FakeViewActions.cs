using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;

namespace DevTools.ModelsContext.Tests.Shared;

public sealed class FakeViewActions(FakeModel model) : IViewActions
{
    public List<VisibilityPlan> ExecutedPlans { get; } = [];

    public List<IReadOnlyList<long>> HiddenReads { get; } = [];

    public HashSet<ElementRef> HiddenLinked { get; } = [];

    public List<IReadOnlyList<ElementRef>> ShownLinked { get; } = [];

    public List<IReadOnlyCollection<ElementRef>> Zoomed { get; } = [];

    public int KeepCount { get; private set; }

    public int ResetCount { get; private set; }

    public Task ExecuteAsync(VisibilityPlan plan, CancellationToken cancellationToken)
    {
        if (plan.IsDisabled)
        {
            throw new InvalidOperationException(plan.DisabledReason);
        }

        ExecutedPlans.Add(plan);
        foreach (var step in plan.Steps.OfType<ReferenceCommandStep>())
        {
            HiddenLinked.UnionWith(step.Ids.Select(id => new ElementRef(step.Document, id)));
        }

        return Task.CompletedTask;
    }

    public Task ShowLinkedAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        ShownLinked.Add(elements.ToList());
        HiddenLinked.ExceptWith(elements);
        return Task.CompletedTask;
    }

    public Task KeepAsync(CancellationToken cancellationToken)
    {
        KeepCount++;
        return Task.CompletedTask;
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        ResetCount++;
        ExecutedPlans.Clear();
        return Task.CompletedTask;
    }

    public Task ZoomAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        Zoomed.Add(elements);
        return Task.CompletedTask;
    }

    public Task SectionBoxAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        return model.View.Is3D
            ? Task.CompletedTask
            : throw new InvalidOperationException("Section box needs a 3D view.");
    }

    public Task<TemporaryState?> ReadTemporaryStateAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<TemporaryState?>(new TemporaryState(model.View, ExecutedPlans.Count > 0, false));
    }

    public Task<IReadOnlyCollection<long>> GetIdsHiddenPermanentlyOrTemporarilyAsync(
        DocumentKey document,
        IReadOnlyCollection<long> candidates,
        CancellationToken cancellationToken)
    {
        HiddenReads.Add(candidates.ToList());
        var snapshot = model.SnapshotOf(document);
        var hidden = new PlanTargets(document, snapshot);
        PlanTargets? kept = null;
        foreach (var plan in ExecutedPlans)
        {
            if (plan.Action == VisibilityAction.Hide)
            {
                hidden.Add(plan);
            }
            else if (plan.Action == VisibilityAction.Isolate)
            {
                kept = new PlanTargets(document, snapshot);
                kept.Add(plan);
            }
        }

        IReadOnlyCollection<long> ids = candidates
            .Where(id => snapshot.TryGetRow(id, out var row)
                         && (hidden.Contains(row) || (kept is not null && !kept.Contains(row))))
            .ToList();
        return Task.FromResult(ids);
    }

    private sealed class PlanTargets(DocumentKey document, DocumentSnapshot snapshot)
    {
        private readonly HashSet<int> _groups = [];
        private readonly HashSet<long> _ids = [];

        public void Add(VisibilityPlan plan)
        {
            foreach (var step in plan.Steps)
            {
                switch (step)
                {
                    case ElementsStep elements when elements.Document == document:
                        _ids.UnionWith(elements.Ids);
                        break;
                    case GroupEverywhereStep group:
                        _groups.Add(snapshot.GroupIndexOf(group.GroupId));
                        break;
                }
            }
        }

        public bool Contains(ElementRow row)
        {
            return _ids.Contains(row.Id) || _groups.Contains(row.GroupIndex);
        }
    }
}
