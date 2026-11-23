using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;

namespace DevTools.ModelsContext.Visibility;

public sealed record VisibilityPlan(
    VisibilityAction Action,
    RgbColor? Color,
    IReadOnlyList<VisibilityStep> Steps,
    string? DisabledReason)
{
    public string? ViewId { get; init; }

    public bool IsDisabled => DisabledReason is not null;

    public bool UsesReferenceCommands => Steps.Any(step => step is ReferenceCommandStep or LinkIsolateStep);

    public bool UsesLinkFilter => Steps.Any(step => step is LinkFilterStep);

    public static VisibilityPlan Disabled(VisibilityAction action, string reason)
    {
        return new VisibilityPlan(action, null, [], reason);
    }
}

public abstract record VisibilityStep;

public sealed record GroupEverywhereStep(long GroupId, string GroupName) : VisibilityStep;

public sealed record AttachedModelStep(DocumentKey Document) : VisibilityStep;

public sealed record ElementsStep(DocumentKey Document, IReadOnlyList<long> Ids) : VisibilityStep;

public sealed record ReferenceCommandStep(DocumentKey Document, IReadOnlyList<long> Ids) : VisibilityStep;

public sealed record LinkIsolateStep(DocumentKey Document, IReadOnlyList<long> KeepIds) : VisibilityStep;

public sealed record LinkFilterStep(DocumentKey Document, IReadOnlyList<long> Ids, FilterDefinition Filter) : VisibilityStep;

public static class VisibilityReasons
{
    public const int LinkFilterCap = 100;
    public const int LinkIsolateCap = 10_000;

    public const string HeaderHasNoActions = "Group headers have no actions.";
    public const string PageRowHasNoActions = "This row has no actions.";
    public const string ReferenceCommandsUnavailable = "Actions on single elements inside a link are not available.";
    public const string ShowFromReveal = "Show these from Reveal Hidden Elements in Revit.";
    public const string NotShown = "Revit did not show these linked elements.";
    public const string LinkedStayHidden = "Revit did not show some linked elements; they stay hidden.";
    public const string LinkOverrideNeedsRule = "Revit can color linked elements only by family, type or a rule filter.";
    public const string LinkFiltersUnavailable = "Halftone and Color inside a link need Revit 2024 or later.";
    public const string LinkRuleNotNative = "Revit cannot draw these rules in a link.";
    public const string LinkFilterNote = "Applies to every By Host View link with this type";
    public const string ViewChanged = "The active view changed.";
    public const string LinksKeepOwnSettings = "Links By Linked View or Custom keep their own settings.";
    public const string RevealOn = "Turn off Reveal Hidden Elements first.";
    public const string NotLoaded = "This model is not loaded.";
    public const string NotSupported = "This model does not support this action.";
    public const string Updating = "Model data is updating.";
    public const string Nothing = "Nothing to act on.";

    public static string LinkFilterTooMany => $"Over {LinkFilterCap} rules to color in a link.";

    public static string LinkIsolateTooMany => $"Over {LinkIsolateCap:N0} linked elements to hide.";

    public static string HostBusy(string hostName)
    {
        return hostName + " is busy.";
    }
}
