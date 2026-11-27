using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;

namespace DevTools.ModelsContext.Visibility;

internal sealed class PlanExecutor(PanelRuntime runtime)
{
    private string? _notice;
    private HiddenProbe? _probe;
    private HiddenRead? _read;

    public async Task<string?> ApplyAsync(
        ViewInfo view,
        ViewLedger ledger,
        VisibilityPlan plan,
        HiddenProbe? probe,
        CancellationToken cancellationToken)
    {
        _probe = probe;
        _read = null;
        if (plan.Action == VisibilityAction.Show)
        {
            return await ShowAsync(view, ledger, plan, cancellationToken);
        }

        var outcome = await ExecuteAsync(view, plan, cancellationToken);
        _notice = outcome.Notice;
        Track(ledger, plan);
        await RecordAsync(view, plan, outcome.LinkHides, cancellationToken);
        return null;
    }

    public async Task<string?> RestoreLinksAsync(ViewInfo view, CancellationToken cancellationToken)
    {
        var entries = runtime.Journal.Get(view.Model, view.ViewId).LinkHides.Select(hide => hide.Element).ToList();
        var gone = entries.Where(element => runtime.Store.Find(element.Document) is null).ToList();
        runtime.Journal.Remove(view.Model, view.ViewId, gone);
        var shown = entries
            .Where(element => CanUnhide(element.Document))
            .ToList();
        string? reason = null;
        if (shown.Count > 0)
        {
            try
            {
                await runtime.Services.ViewActions.ShowLinkedAsync(shown, cancellationToken);
            }
            catch (LinkedNotShownException)
            {
                reason = VisibilityText.LinkedStayHidden;
            }
        }

        runtime.Journal.ClearChanges(view.Model, view.ViewId);
        runtime.Journal.Remove(view.Model, view.ViewId, shown);
        return reason;
    }

    private static void Track(ViewLedger ledger, VisibilityPlan plan)
    {
        switch (plan.Action)
        {
            case VisibilityAction.Hide:
                ledger.Add(plan);
                break;
            case VisibilityAction.Isolate:
                ledger.Isolation = Isolation.From(plan);
                break;
            default:
                ledger.OverridePlans.Add(plan);
                ledger.AddOverrides(plan);
                break;
        }
    }

    private async Task<string?> ShowAsync(
        ViewInfo view,
        ViewLedger ledger,
        VisibilityPlan plan,
        CancellationToken cancellationToken)
    {
        var journaled = JournaledOf(view);
        var linked = plan.Steps.OfType<LinkElementsStep>()
            .SelectMany(step => step.Ids.Select(id => new ElementRef(step.Document, id)))
            .Where(journaled.Contains)
            .ToList();
        var temporary = plan.Steps.Any(step => step is not LinkElementsStep);
        if (!temporary && linked.Count == 0)
        {
            return VisibilityText.Nothing;
        }

        if (temporary)
        {
            await ShowTemporaryAsync(view, ledger, plan, cancellationToken);
        }

        if (linked.Count > 0)
        {
            await ShowLinkedAsync(view, linked, cancellationToken);
        }

        return null;
    }

    private HashSet<ElementRef> JournaledOf(ViewInfo view)
    {
        return runtime.Journal.Get(view.Model, view.ViewId).AllLinkHides
            .Select(hide => hide.Element)
            .ToHashSet();
    }

    private async Task ShowLinkedAsync(
        ViewInfo view,
        IReadOnlyList<ElementRef> linked,
        CancellationToken cancellationToken)
    {
        await runtime.Services.ViewActions.ShowLinkedAsync(linked, cancellationToken);
        runtime.Journal.Remove(view.Model, view.ViewId, linked);
    }

    private async Task ShowTemporaryAsync(
        ViewInfo view,
        ViewLedger ledger,
        VisibilityPlan plan,
        CancellationToken cancellationToken)
    {
        var rest = runtime.Journal.Get(view.Model, view.ViewId).LinkHides
            .Where(hide => hide.IsIsolateRest && CanUnhide(hide.Element.Document))
            .Select(hide => hide.Element)
            .ToList();
        _read = await runtime.Services.ViewActions.ResetAsync(view.ViewId, _probe, cancellationToken);
        ledger.Remove(plan);
        ledger.Isolation = null;
        _notice = rest.Count > 0 ? await ShowRestAsync(view, rest, cancellationToken) : null;
        runtime.Journal.ClearChanges(view.Model, view.ViewId);
        await ReapplyAsync(view, ledger, cancellationToken);
    }

    public string? TakeNotice()
    {
        var notice = _notice;
        _notice = null;
        return notice;
    }

    public HiddenRead? TakeRead()
    {
        var read = _read;
        _read = null;
        _probe = null;
        return read;
    }

    private async Task<PlanOutcome> ExecuteAsync(ViewInfo view, VisibilityPlan plan, CancellationToken cancellationToken)
    {
        var outcome = await runtime.Services.ViewActions.ExecuteAsync(
            plan with { ViewId = view.ViewId, ReadBack = _probe }, cancellationToken);
        _read = outcome.ReadBack;
        return outcome;
    }

    private async Task<string?> ShowRestAsync(
        ViewInfo view,
        IReadOnlyList<ElementRef> rest,
        CancellationToken cancellationToken)
    {
        try
        {
            await ShowLinkedAsync(view, rest, cancellationToken);
            return null;
        }
        catch (LinkedNotShownException)
        {
            runtime.Journal.Remove(view.Model, view.ViewId, rest);
            return VisibilityText.LinkedStayHidden;
        }
    }

    private bool CanUnhide(DocumentKey document)
    {
        return runtime.Store.Find(document) is { } info
               && info.Can(DocumentCapabilities.LinkElements | DocumentCapabilities.LinkShow);
    }

    private async Task ReapplyAsync(ViewInfo view, ViewLedger ledger, CancellationToken cancellationToken)
    {
        if (ledger.HidePlan() is { } hide)
        {
            var outcome = await ExecuteAsync(view, hide, cancellationToken);
            await RecordAsync(view, hide, outcome.LinkHides, cancellationToken);
        }

        foreach (var plan in ledger.OverridePlans)
        {
            var outcome = await ExecuteAsync(view, plan, cancellationToken);
            await RecordAsync(view, plan, outcome.LinkHides, cancellationToken);
        }
    }

    private async Task RecordAsync(
        ViewInfo view,
        VisibilityPlan plan,
        IReadOnlyList<ElementRef> hidden,
        CancellationToken cancellationToken)
    {
        var changed = plan.Steps.Any(step => step is not LinkElementsStep);
        if (changed)
        {
            runtime.Journal.AddChange(view.Model, view.ViewId, plan.KeepConverts);
        }

        var isolate = plan.Action == VisibilityAction.Isolate;
        if (isolate)
        {
            runtime.Journal.EndIsolateRest(view.Model, view.ViewId);
        }

        var hides = new List<LinkHide>();
        foreach (var group in hidden.Where(element => !element.Document.IsCurrentModel).GroupBy(element => element.Document))
        {
            var ids = group.Select(element => element.Id).ToList();
            var uniqueIds = await runtime.Services.Source.GetUniqueIdsAsync(group.Key, ids, cancellationToken);
            hides.AddRange(ids.Select(id => new LinkHide(new ElementRef(group.Key, id),
                uniqueIds.TryGetValue(id, out var uniqueId) ? uniqueId : string.Empty, isolate)));
        }

        if (hides.Count > 0)
        {
            runtime.Journal.AddLinkHides(view.Model, view.ViewId, hides, changed);
        }
    }
}
