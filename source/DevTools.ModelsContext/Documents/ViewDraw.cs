using System.Globalization;

namespace DevTools.ModelsContext.Documents;

public enum ViewKind
{
    Other,
    Plan,
    CeilingPlan,
    AreaPlan,
    Section,
    ThreeD
}

[Flags]
public enum ViewDraw : byte
{
    None = 0,
    Plan = 1,
    CeilingPlan = 2,
    AreaPlan = 4,
    Section = 8,
    ThreeD = 16,
    Spatial = Plan | CeilingPlan | AreaPlan | Section,
    Model = Spatial | ThreeD
}

public static class ViewDraws
{
    public const long NoOwner = -1;

    public static ViewDraw Of(ViewKind kind)
    {
        return kind switch
        {
            ViewKind.Plan => ViewDraw.Plan,
            ViewKind.CeilingPlan => ViewDraw.CeilingPlan,
            ViewKind.AreaPlan => ViewDraw.AreaPlan,
            ViewKind.Section => ViewDraw.Section,
            ViewKind.ThreeD => ViewDraw.ThreeD,
            _ => ViewDraw.None
        };
    }

    public static bool CanDraw(ViewInfo? view, ElementRow row, bool currentModel)
    {
        if (row.OwnerViewId != NoOwner)
        {
            return currentModel && view is not null
                                && long.TryParse(view.ViewId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                                && id == row.OwnerViewId;
        }

        return view is null ? (row.Draw & ViewDraw.Model) != 0 : Draws(row.Draw, view.Kind);
    }

    public static bool Draws(ViewDraw draw, ViewKind kind)
    {
        return (draw & Of(kind)) != 0;
    }
}
