using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Shared;

public sealed class FakeViewActions(FakeModel model) : IViewActions
{
    public List<VisibilityPlan> ExecutedPlans { get; } = [];

    public List<IReadOnlyList<long>> ReadBacks { get; } = [];

    public List<IReadOnlyList<ElementRef>> ShownLinked { get; } = [];

    public List<IReadOnlyCollection<ElementRef>> Zoomed { get; } = [];

    public int KeepCount { get; private set; }

    public int ResetCount { get; private set; }

    public bool LinkedNotShown { get; set; }

    public string? Refusal { get; set; }

    public string? Notice { get; set; }

    public Task<PlanOutcome> ExecuteAsync(VisibilityPlan plan, CancellationToken cancellationToken)
    {
        if (plan.IsDisabled)
        {
            throw new InvalidOperationException(plan.DisabledReason);
        }

        RequireView(plan.ViewId);
        if (Refusal is { } refusal)
        {
            throw new InvalidOperationException(refusal);
        }

        ExecutedPlans.Add(plan);
        var hidden = new List<ElementRef>();
        foreach (var step in plan.Steps)
        {
            switch (step)
            {
                case LinkElementsStep references:
                    hidden.AddRange(references.Ids.Select(id => new ElementRef(references.Document, id))
                        .Where(element => !model.ViewNotDrawn.Contains(element)));
                    break;
                case LinkIsolateStep isolate:
                    var keep = isolate.KeptIds.ToHashSet();
                    hidden.AddRange(model.SnapshotOf(isolate.Document).Rows
                        .Where(row => !keep.Contains(row.Id))
                        .Select(row => new ElementRef(isolate.Document, row.Id)));
                    break;
            }
        }

        model.ViewHiddenElements.UnionWith(hidden);
        var categories = plan.Steps.OfType<CategoryStep>().Select(step => step.CategoryId).ToList();
        if (plan.Action == VisibilityAction.Hide)
        {
            model.ViewHiddenCategories.UnionWith(categories);
        }
        else if (plan.Action == VisibilityAction.Isolate && categories.Count > 0)
        {
            model.ViewKeptCategories = [.. categories];
        }

        var notice = Notice;
        Notice = null;
        return Task.FromResult(new PlanOutcome(hidden, notice, ReadBackOf(plan.ReadBack)));
    }

    public Task ShowLinkedAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        ShownLinked.Add(elements.ToList());
        if (LinkedNotShown)
        {
            throw new LinkedNotShownException();
        }

        model.ViewHiddenElements.ExceptWith(elements);
        return Task.CompletedTask;
    }

    public Task KeepAsync(string? viewId, CancellationToken cancellationToken)
    {
        RequireView(viewId);
        KeepCount++;
        return Task.CompletedTask;
    }

    public string? ResetError { get; set; }

    public Task<ReadBackResult?> ResetAsync(string? viewId, ReadBackRequest? request, CancellationToken cancellationToken)
    {
        if (ResetError is { } error)
        {
            throw new InvalidOperationException(error);
        }

        RequireView(viewId);
        ResetCount++;
        ExecutedPlans.Clear();
        model.ViewHiddenCategories.Clear();
        model.ViewKeptCategories = null;
        return Task.FromResult(ReadBackOf(request));
    }

    public int ActionReads { get; private set; }

    private ReadBackResult? ReadBackOf(ReadBackRequest? request)
    {
        if (request is not { Ids.Count: > 0 })
        {
            return null;
        }

        ActionReads++;
        return new ReadBackResult(request.Document, request.Ids, HiddenOf(request.Document, request.Ids));
    }

    private void RequireView(string? viewId)
    {
        if (viewId is not null && viewId != model.View.ViewId)
        {
            throw new ViewChangedException();
        }
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
            : throw new InvalidOperationException(VisibilityText.Needs3DView);
    }

    public Task<ActiveView?> ReadActiveViewAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<ActiveView?>(new ActiveView(model.View, ExecutedPlans.Count > 0));
    }

    public TaskCompletionSource<bool>? HoldNextReadBack { get; set; }

    public async Task<ReadBackResult> ReadBackAsync(ReadBackRequest request, CancellationToken cancellationToken)
    {
        ReadBacks.Add(request.Ids.ToList());
        if (HoldNextReadBack is { } hold)
        {
            HoldNextReadBack = null;
            await hold.Task;
        }

        return new ReadBackResult(request.Document, request.Ids, HiddenOf(request.Document, request.Ids));
    }

    private IReadOnlyCollection<long> HiddenOf(DocumentKey document, IReadOnlyCollection<long> candidates)
    {
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

        return candidates
            .Where(id => !model.ViewNotDrawn.Contains(new ElementRef(document, id))
                         && snapshot.TryGetRow(id, out var row)
                         && (hidden.Contains(row) || (kept is not null && !kept.Contains(row))))
            .ToList();
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
                    case CategoryStep group:
                        _groups.Add(snapshot.GroupIndexOf(group.CategoryId));
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
