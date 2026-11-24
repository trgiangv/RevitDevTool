namespace DevTools.ModelsContext.Visibility;

public enum HiddenCause
{
    Temporary,
    InView,
    Category,
    Filter,
    FilterPossible,
    Workset,
    Phase,
    DesignOption,
    AboveRange,
    BelowRange,
    OutsideCrop,
    OwnerView,
    LinkInstance,
    LinkCategory,
    LinkWorkset,
    LinkDisplayMode,
    LinkElement,
    NotDrawn,
    None
}

public sealed record HiddenReason(HiddenCause Cause, string Text, bool CanShow = false);

public static class HiddenReasons
{
    public static HiddenReason Temporary { get; } = new(HiddenCause.Temporary, "Temporary hide/isolate", true);

    public static HiddenReason LinkTemporary { get; } = new(HiddenCause.Temporary, "Link hidden by temporary hide/isolate");

    public static HiddenReason InView { get; } = new(HiddenCause.InView, "Hidden in view");

    public static HiddenReason LinkElement { get; } = new(HiddenCause.LinkElement, "Hidden in view", true);

    public static HiddenReason LinkInstance { get; } = new(HiddenCause.LinkInstance, "Link hidden in view");

    public static HiddenReason AboveCutPlane { get; } = new(HiddenCause.AboveRange, "Above cut plane");

    public static HiddenReason AboveViewRange { get; } = new(HiddenCause.AboveRange, "Above view range");

    public static HiddenReason BelowViewDepth { get; } = new(HiddenCause.BelowRange, "Below view depth");

    public static HiddenReason BelowCutPlane { get; } = new(HiddenCause.BelowRange, "Below cut plane");

    public static HiddenReason NoGeometry { get; } = new(HiddenCause.NotDrawn, "No geometry to draw");

    public static HiddenReason NotInViewType { get; } = new(HiddenCause.NotDrawn, "Not drawn in this view type");

    public static HiddenReason OutsideCrop { get; } = new(HiddenCause.OutsideCrop, "Outside crop");

    public static HiddenReason OutsideSectionBox { get; } = new(HiddenCause.OutsideCrop, "Outside section box");

    public static HiddenReason NotModelView { get; } = new(HiddenCause.None, "Not a model view");

    public static HiddenReason None { get; } = new(HiddenCause.None, "No view setting hides it");

    public static HiddenReason Category(string name)
    {
        return new HiddenReason(HiddenCause.Category, "Category off: " + name);
    }

    public static HiddenReason LinkCategory(string name)
    {
        return new HiddenReason(HiddenCause.LinkCategory, "Category off: " + name);
    }

    public static HiddenReason Filter(string name)
    {
        return new HiddenReason(HiddenCause.Filter, "Filter: " + name);
    }

    public static HiddenReason FilterPossible(string name)
    {
        return new HiddenReason(HiddenCause.FilterPossible, $"Filter: {name} (possible)");
    }

    public static HiddenReason WorksetHidden(string name)
    {
        return new HiddenReason(HiddenCause.Workset, "Workset hidden: " + name);
    }

    public static HiddenReason WorksetClosed(string name)
    {
        return new HiddenReason(HiddenCause.Workset, "Workset closed: " + name);
    }

    public static HiddenReason LinkWorkset(string name)
    {
        return new HiddenReason(HiddenCause.LinkWorkset, "Link workset hidden: " + name);
    }

    public static HiddenReason CreatedLater(string phase)
    {
        return new HiddenReason(HiddenCause.Phase, "Phase: created in " + phase);
    }

    public static HiddenReason Demolished(string phase)
    {
        return new HiddenReason(HiddenCause.Phase, "Phase: demolished in " + phase);
    }

    public static HiddenReason DesignOption(string name)
    {
        return new HiddenReason(HiddenCause.DesignOption, "Design option: " + name);
    }

    public static HiddenReason OwnerView(string name)
    {
        return new HiddenReason(HiddenCause.OwnerView, "Owned by view: " + name);
    }

    public static HiddenReason FamilySettings(string family)
    {
        return new HiddenReason(HiddenCause.NotDrawn, "Hidden by its family: " + family);
    }

    public static HiddenReason LinkDisplayMode(string mode)
    {
        return new HiddenReason(HiddenCause.LinkDisplayMode, mode + ": link's own settings");
    }
}
