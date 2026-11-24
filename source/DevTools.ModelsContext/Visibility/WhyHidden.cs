using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Visibility;

public sealed record WhyHidden(IReadOnlyList<HiddenReason> Reasons, double ElapsedMs)
{
    private static readonly HiddenCause[] Order =
    [
        HiddenCause.LinkInstance,
        HiddenCause.LinkCategory,
        HiddenCause.LinkWorkset,
        HiddenCause.LinkDisplayMode,
        HiddenCause.LinkElement,
        HiddenCause.Temporary,
        HiddenCause.InView,
        HiddenCause.Category,
        HiddenCause.Filter,
        HiddenCause.FilterPossible,
        HiddenCause.Workset,
        HiddenCause.Phase,
        HiddenCause.DesignOption,
        HiddenCause.NotDrawn,
        HiddenCause.OwnerView,
        HiddenCause.AboveRange,
        HiddenCause.BelowRange,
        HiddenCause.OutsideCrop,
        HiddenCause.None
    ];

    public HiddenReason First => Reasons[0];

    public bool Found => First.Cause != HiddenCause.None;

    public static WhyHidden Of(IEnumerable<HiddenReason> reasons, double elapsedMs)
    {
        var ordered = reasons.Distinct()
            .Select((reason, index) => (Reason: reason, Index: index))
            .OrderBy(pair => Array.IndexOf(Order, pair.Reason.Cause))
            .ThenBy(pair => pair.Index)
            .Select(pair => pair.Reason)
            .ToList();
        var found = ordered.Where(reason => reason.Cause != HiddenCause.None).ToList();
        if (found.Count > 0)
        {
            return new WhyHidden(found, elapsedMs);
        }

        return new WhyHidden([ordered.FirstOrDefault() ?? HiddenReasons.None], elapsedMs);
    }

    public WhyHidden WithJournal(JournalPage journal, ElementRef element, bool hidByPanel)
    {
        if (journal.AllLinkHides.Any(hide => hide.Element == element))
        {
            return Of([HiddenReasons.LinkElement, .. Reasons], ElapsedMs);
        }

        return hidByPanel || Reasons.All(reason => !reason.CanShow)
            ? this
            : new WhyHidden(Reasons.Select(reason => reason with { CanShow = false }).ToList(), ElapsedMs);
    }
}
