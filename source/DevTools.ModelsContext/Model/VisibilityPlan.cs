namespace DevTools.ModelsContext.Model;

public sealed record VisibilityPlan(
    VisibilityAction Action,
    RgbColor? Color,
    IReadOnlyList<VisibilityStep> Steps,
    string? DisabledReason)
{
    public bool IsDisabled => DisabledReason is not null;

    public bool UsesReferenceCommands => Steps.Any(step => step is ReferenceCommandStep);

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

public static class VisibilityReasons
{
    public const string HeaderHasNoActions = "Group headers have no actions.";
    public const string PageRowHasNoActions = "This row has no actions.";
    public const string LinkChildOnlyShowIsolate = "Halftone and Color for a link are on its model row.";
    public const string ReferenceCommandsUnavailable = "Actions on single elements inside a link are not available.";
    public const string NotLoaded = "This model is not loaded.";
    public const string NotSupported = "This model does not support this action.";
    public const string Updating = "Model data is updating.";
    public const string Nothing = "Nothing to act on.";
}
