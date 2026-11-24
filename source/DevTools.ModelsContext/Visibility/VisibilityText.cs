namespace DevTools.ModelsContext.Visibility;

public static class VisibilityText
{
    public const string HeaderHasNoActions = "Group headers have no actions.";
    public const string PageRowHasNoActions = "This row has no actions.";
    public const string LinkElementsUnavailable = "Actions on single elements inside a link are not available.";
    public const string ShowFromReveal = "Show these from Reveal Hidden Elements in Revit.";
    public const string NotHidden = "Revit did not hide these linked elements.";
    public const string LinkedStayHidden = "Revit did not show some linked elements; they stay hidden.";
    public const string LinkOverrideNeedsRule = "Revit can color linked elements only by family, type or a rule filter.";
    public const string LinkFiltersUnavailable = "Halftone and Color inside a link need Revit 2024 or later.";
    public const string LinkRuleNotNative = "Revit cannot draw these rules in a link.";
    public const string NoNameRule = "Revit cannot filter this category by family or type.";
    public const string NotByHostView = "Revit colors linked elements only when the link is By Host View.";
    public const string NotFilterable = "Revit cannot use this category in a view filter.";
    public const string NotDeleted = "Could not delete the view filter {0}.";
    public const string NoLinkView = "This view cannot show linked elements.";
    public const string LinkFilterNote = "Applies to every By Host View link with this type";
    public const string SavedWithView = "Saved with the view";
    public const string ViewChanged = "The active view changed.";
    public const string ModelChanged = "The active model changed.";
    public const string NoActiveView = "There is no active view.";
    public const string NoTemporaryProperties = "This view cannot take temporary overrides.";
    public const string Needs3DView = "Section box needs a 3D view.";
    public const string NothingToFrame = "Nothing to frame.";
    public const string ElementGone = "This element is no longer in the model.";
    public const string LinksKeepOwnSettings = "Links By Linked View or Custom keep their own settings.";
    public const string RevealOn = "Turn off Reveal Hidden Elements first.";
    public const string NotLoaded = "This model is not loaded.";
    public const string NotSupported = "This model does not support this action.";
    public const string Updating = "Model data is updating.";
    public const string Nothing = "Nothing to act on.";

    public static string LinkFilterTooMany => $"Over {LinkRules.MaxRules} rules to color in a link.";

    public static string LinkIsolateTooMany => $"Over {LinkRules.MaxIsolateRest:N0} linked elements to hide.";

    public static string HostBusy(string hostName)
    {
        return hostName + " is busy.";
    }
}
