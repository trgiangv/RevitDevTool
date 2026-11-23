using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;

namespace DevTools.ModelsContext.Visibility;

internal sealed class PlanExecutor(PanelRuntime runtime)
{
    private string? _notice;

    public async Task<string?> ApplyAsync(
        ViewInfo view,
        ViewLedger ledger,
        VisibilityPlan plan,
        CancellationToken cancellationToken)
    {
        if (plan.Action == VisibilityAction.Show)
        {
            return await ShowAsync(view, ledger, plan, cancellationToken);
        }

        var hidden = await runtime.Services.ViewActions.ExecuteAsync(plan with { ViewId = view.ViewId }, cancellationToken);
        _notice = runtime.Services.ViewActions.TakeNotice();
        Track(ledger, plan, hidden);
        await RecordAsync(view, plan, hidden, cancellationToken);
        return null;
    }

    public async Task<string?> RestoreLinksAsync(ViewInfo view, CancellationToken cancellationToken)
    {
        var shown = runtime.Journal.Get(view.Model, view.ViewId).LinkEntries
            .Select(entry => entry.Element)
            .Where(element => CanUnhide(element.Document))
            .ToList();
        string? reason = null;
        if (shown.Count > 0)
        {
            try
            {
                await runtime.Services.ViewActions.ShowLinkedAsync(shown, cancellationToken);
            }
            catch (InvalidOperationException exception) when (exception.Message == VisibilityReasons.NotShown)
            {
                reason = VisibilityReasons.LinkedStayHidden;
            }
        }

        runtime.Journal.ResetTemporary(view.Model, view.ViewId);
        runtime.Journal.Remove(view.Model, view.ViewId, shown);
        return reason;
    }

    private static void Track(ViewLedger ledger, VisibilityPlan plan, IReadOnlyList<ElementRef> hidden)
    {
        switch (plan.Action)
        {
            case VisibilityAction.Hide:
                ledger.Add(plan);
                break;
            case VisibilityAction.Isolate:
                ledger.Isolation = LedgerIsolation.From(plan, hidden);
                ledger.Elements.UnionWith(hidden);
                break;
            default:
                ledger.Appearance.Add(plan);
                ledger.Mark(plan);
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
        var linked = plan.Steps.OfType<ReferenceCommandStep>()
            .SelectMany(step => step.Ids.Select(id => new ElementRef(step.Document, id)))
            .Where(journaled.Contains)
            .ToList();
        var temporary = plan.Steps.Any(step => step is not ReferenceCommandStep);
        if (!temporary && linked.Count == 0)
        {
            return VisibilityReasons.Nothing;
        }

        if (temporary)
        {
            await ShowTemporaryAsync(view, ledger, plan, cancellationToken);
        }

        if (linked.Count > 0)
        {
            await ShowLinkedAsync(view, ledger, linked, cancellationToken);
        }

        return null;
    }

    private HashSet<ElementRef> JournaledOf(ViewInfo view)
    {
        return runtime.Journal.Get(view.Model, view.ViewId).LinkEntries
            .Select(entry => entry.Element)
            .ToHashSet();
    }

    private async Task ShowLinkedAsync(
        ViewInfo view,
        ViewLedger ledger,
        IReadOnlyList<ElementRef> linked,
        CancellationToken cancellationToken)
    {
        await runtime.Services.ViewActions.ShowLinkedAsync(linked, cancellationToken);
        ledger.Elements.ExceptWith(linked);
        runtime.Journal.Remove(view.Model, view.ViewId, linked);
    }

    private async Task ShowTemporaryAsync(
        ViewInfo view,
        ViewLedger ledger,
        VisibilityPlan plan,
        CancellationToken cancellationToken)
    {
        var journaled = JournaledOf(view);
        var rest = ledger.Isolation?.Rest
            .Where(element => journaled.Contains(element) && CanUnhide(element.Document))
            .ToList() ?? [];
        ledger.Remove(plan);
        ledger.Isolation = null;
        await runtime.Services.ViewActions.ResetAsync(cancellationToken);
        _notice = rest.Count > 0 ? await ShowRestAsync(view, ledger, rest, cancellationToken) : null;
        runtime.Journal.ResetTemporary(view.Model, view.ViewId);
        await ReapplyAsync(view, ledger, cancellationToken);
    }

    public string? TakeNotice()
    {
        var notice = _notice;
        _notice = null;
        return notice;
    }

    private async Task<string?> ShowRestAsync(
        ViewInfo view,
        ViewLedger ledger,
        IReadOnlyList<ElementRef> rest,
        CancellationToken cancellationToken)
    {
        try
        {
            await ShowLinkedAsync(view, ledger, rest, cancellationToken);
            return null;
        }
        catch (InvalidOperationException exception) when (exception.Message == VisibilityReasons.NotShown)
        {
            ledger.Elements.ExceptWith(rest);
            runtime.Journal.Remove(view.Model, view.ViewId, rest);
            return VisibilityReasons.LinkedStayHidden;
        }
    }

    private bool CanUnhide(DocumentKey document)
    {
        return runtime.Store.Find(document) is { } info
               && info.Can(DocumentCapabilities.ReferenceCommands | DocumentCapabilities.ReferenceUnhide);
    }

    private async Task ReapplyAsync(ViewInfo view, ViewLedger ledger, CancellationToken cancellationToken)
    {
        var actions = runtime.Services.ViewActions;
        if (ledger.HidePlan() is { } hide)
        {
            var hidden = await actions.ExecuteAsync(hide with { ViewId = view.ViewId }, cancellationToken);
            await RecordAsync(view, hide, hidden, cancellationToken);
        }

        foreach (var plan in ledger.Appearance)
        {
            var hidden = await actions.ExecuteAsync(plan with { ViewId = view.ViewId }, cancellationToken);
            await RecordAsync(view, plan, hidden, cancellationToken);
        }
    }

    private async Task RecordAsync(
        ViewInfo view,
        VisibilityPlan plan,
        IReadOnlyList<ElementRef> hidden,
        CancellationToken cancellationToken)
    {
        if (plan.Steps.Any(step => step is not ReferenceCommandStep))
        {
            runtime.Journal.Record(view.Model, view.ViewId);
        }

        var entries = new List<JournalEntry>();
        foreach (var group in hidden.Where(element => !element.Document.IsCurrentModel).GroupBy(element => element.Document))
        {
            var ids = group.Select(element => element.Id).ToList();
            var uniqueIds = await runtime.Services.Source.GetUniqueIdsAsync(group.Key, ids, cancellationToken);
            entries.AddRange(ids.Select(id => new JournalEntry(new ElementRef(group.Key, id),
                uniqueIds.TryGetValue(id, out var uniqueId) ? uniqueId : string.Empty)));
        }

        if (entries.Count > 0)
        {
            runtime.Journal.RecordHidden(view.Model, view.ViewId, entries);
        }
    }
}
