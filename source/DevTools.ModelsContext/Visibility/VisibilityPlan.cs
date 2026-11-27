using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Visibility;

public sealed record VisibilityPlan(
    VisibilityAction Action,
    RgbColor? Color,
    IReadOnlyList<PlanStep> Steps,
    string? DisabledReason)
{
    public string? ViewId { get; init; }

    public ReadBackRequest? ReadBack { get; init; }

    public bool IsDisabled => DisabledReason is not null;

    public bool UsesLinkHide => Steps.Any(step => step is LinkElementsStep or LinkIsolateStep);

    public bool KeepConverts => Action == VisibilityAction.Hide
                                || (Action == VisibilityAction.Isolate && !Steps.Any(step => step is CategoryStep));

    public VisibilityPlan Renamed(ModelRename rename)
    {
        return this with
        {
            Steps = Steps.Select(step => step is DocumentStep document
                ? document with { Document = rename.Map(document.Document) }
                : step).ToList()
        };
    }

    public static VisibilityPlan Disabled(VisibilityAction action, string reason)
    {
        return new VisibilityPlan(action, null, [], reason);
    }
}
