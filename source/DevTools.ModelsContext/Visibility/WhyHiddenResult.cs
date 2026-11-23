using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Visibility;

public sealed record WhyHiddenResult(IReadOnlyList<HiddenCause> Causes, double ElapsedMs)
{
    private static readonly HiddenCauseKind[] Order =
    [
        HiddenCauseKind.LinkHidden,
        HiddenCauseKind.LinkCategory,
        HiddenCauseKind.LinkWorkset,
        HiddenCauseKind.LinkDisplayMode,
        HiddenCauseKind.HiddenInLink,
        HiddenCauseKind.TemporaryHideIsolate,
        HiddenCauseKind.HiddenInView,
        HiddenCauseKind.Category,
        HiddenCauseKind.Filter,
        HiddenCauseKind.FilterPossible,
        HiddenCauseKind.Workset,
        HiddenCauseKind.Phase,
        HiddenCauseKind.DesignOption,
        HiddenCauseKind.NotDrawn,
        HiddenCauseKind.OwnerView,
        HiddenCauseKind.AboveCutPlane,
        HiddenCauseKind.BelowViewDepth,
        HiddenCauseKind.OutsideCrop,
        HiddenCauseKind.NoneFound
    ];

    public HiddenCause First => Causes[0];

    public bool Found => First.Kind != HiddenCauseKind.NoneFound;

    public static WhyHiddenResult Of(IEnumerable<HiddenCause> causes, double elapsedMs)
    {
        var ordered = causes.Distinct()
            .Select((cause, index) => (Cause: cause, Index: index))
            .OrderBy(pair => Array.IndexOf(Order, pair.Cause.Kind))
            .ThenBy(pair => pair.Index)
            .Select(pair => pair.Cause)
            .ToList();
        var found = ordered.Where(cause => cause.Kind != HiddenCauseKind.NoneFound).ToList();
        if (found.Count > 0)
        {
            return new WhyHiddenResult(found, elapsedMs);
        }

        return new WhyHiddenResult([ordered.FirstOrDefault() ?? HiddenCues.NoneFound], elapsedMs);
    }

    public WhyHiddenResult WithJournal(JournalState journal, ElementRef element)
    {
        return journal.LinkEntries.Any(entry => entry.Element == element)
            ? Of([HiddenCues.HiddenInLink, .. Causes], ElapsedMs)
            : this;
    }
}
