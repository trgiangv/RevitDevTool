using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Explorer;

public readonly record struct RowOverride(RgbColor? Color, bool Halftone)
{
    public RowOverride With(VisibilityPlan plan)
    {
        return plan.Action == VisibilityAction.Halftone
            ? this with { Halftone = true }
            : this with { Color = plan.Color ?? Color };
    }
}
