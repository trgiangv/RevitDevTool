namespace DevTools.ModelsContext.Visibility;

public enum HiddenCauseKind
{
    TemporaryHideIsolate,
    HiddenInView,
    Category,
    Filter,
    FilterPossible,
    Workset,
    Phase,
    DesignOption,
    AboveCutPlane,
    BelowViewDepth,
    OutsideCrop,
    OwnerView,
    LinkHidden,
    LinkCategory,
    LinkWorkset,
    LinkDisplayMode,
    HiddenInLink,
    NotDrawn,
    NoneFound
}

public enum HiddenFix
{
    None,
    ShowElement
}

public sealed record HiddenCause(HiddenCauseKind Kind, string Text, HiddenFix Fix = HiddenFix.None);

public static class HiddenCues
{
    public static HiddenCause TemporaryHideIsolate { get; } = new(HiddenCauseKind.TemporaryHideIsolate, "Temporary hide/isolate", HiddenFix.ShowElement);

    public static HiddenCause LinkTemporary { get; } = new(HiddenCauseKind.TemporaryHideIsolate, "Link hidden by temporary hide/isolate");

    public static HiddenCause HiddenInView { get; } = new(HiddenCauseKind.HiddenInView, "Hidden in view");

    public static HiddenCause HiddenInLink { get; } = new(HiddenCauseKind.HiddenInLink, "Hidden in view", HiddenFix.ShowElement);

    public static HiddenCause LinkHidden { get; } = new(HiddenCauseKind.LinkHidden, "Link hidden in view");

    public static HiddenCause AboveCutPlane { get; } = new(HiddenCauseKind.AboveCutPlane, "Above cut plane");

    public static HiddenCause AboveViewRange { get; } = new(HiddenCauseKind.AboveCutPlane, "Above view range");

    public static HiddenCause BelowViewDepth { get; } = new(HiddenCauseKind.BelowViewDepth, "Below view depth");

    public static HiddenCause BelowCutPlane { get; } = new(HiddenCauseKind.BelowViewDepth, "Below cut plane");

    public static HiddenCause NoGeometry { get; } = new(HiddenCauseKind.NotDrawn, "No geometry to draw");

    public static HiddenCause NotInViewType { get; } = new(HiddenCauseKind.NotDrawn, "Not drawn in this view type");

    public static HiddenCause BeyondFarClip { get; } = new(HiddenCauseKind.BelowViewDepth, "Beyond far clip");

    public static HiddenCause OutsideCrop { get; } = new(HiddenCauseKind.OutsideCrop, "Outside crop");

    public static HiddenCause OutsideSectionBox { get; } = new(HiddenCauseKind.OutsideCrop, "Outside section box");

    public static HiddenCause NotModelView { get; } = new(HiddenCauseKind.NoneFound, "Not a model view");

    public static HiddenCause NoneFound { get; } = new(HiddenCauseKind.NoneFound, "No view setting hides it");

    public static HiddenCause Category(string name)
    {
        return new HiddenCause(HiddenCauseKind.Category, "Category off: " + name);
    }

    public static HiddenCause LinkCategory(string name)
    {
        return new HiddenCause(HiddenCauseKind.LinkCategory, "Category off: " + name);
    }

    public static HiddenCause Filter(string name)
    {
        return new HiddenCause(HiddenCauseKind.Filter, "Filter: " + name);
    }

    public static HiddenCause FilterPossible(string name)
    {
        return new HiddenCause(HiddenCauseKind.FilterPossible, $"Filter: {name} (possible)");
    }

    public static HiddenCause WorksetHidden(string name)
    {
        return new HiddenCause(HiddenCauseKind.Workset, "Workset hidden: " + name);
    }

    public static HiddenCause WorksetClosed(string name)
    {
        return new HiddenCause(HiddenCauseKind.Workset, "Workset closed: " + name);
    }

    public static HiddenCause LinkWorkset(string name)
    {
        return new HiddenCause(HiddenCauseKind.LinkWorkset, "Link workset hidden: " + name);
    }

    public static HiddenCause CreatedLater(string phase)
    {
        return new HiddenCause(HiddenCauseKind.Phase, "Phase: created in " + phase);
    }

    public static HiddenCause Demolished(string phase)
    {
        return new HiddenCause(HiddenCauseKind.Phase, "Phase: demolished in " + phase);
    }

    public static HiddenCause DesignOption(string name)
    {
        return new HiddenCause(HiddenCauseKind.DesignOption, "Design option: " + name);
    }

    public static HiddenCause OwnerView(string name)
    {
        return new HiddenCause(HiddenCauseKind.OwnerView, "Owned by view: " + name);
    }

    public static HiddenCause FamilySettings(string family)
    {
        return new HiddenCause(HiddenCauseKind.NotDrawn, "Hidden by its family: " + family);
    }

    public static HiddenCause LinkDisplayMode(string mode)
    {
        return new HiddenCause(HiddenCauseKind.LinkDisplayMode, mode + ": link's own settings");
    }
}
