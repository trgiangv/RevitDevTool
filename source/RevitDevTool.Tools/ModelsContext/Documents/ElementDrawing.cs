using DevTools.ModelsContext.Documents;
using Nice3point.Revit.Extensions;
using Area = Autodesk.Revit.DB.Area;

namespace RevitDevTool.Tools.ModelsContext.Documents;

internal static class ElementDrawing
{
    public static ViewDraw DrawOf(Element element)
    {
        if (element is View || element.OwnerViewId != ElementId.InvalidElementId)
        {
            return ViewDraw.None;
        }

        if (element is Area)
        {
            return ViewDraw.AreaPlan;
        }

        if (element.get_BoundingBox(null) is null)
        {
            return ViewDraw.None;
        }

        return element is SpatialElement ? ViewDraw.Spatial : ViewDraw.Model;
    }

    public static long OwnerOf(Element element)
    {
        return element.OwnerViewId == ElementId.InvalidElementId ? ViewDraws.NoOwner : element.OwnerViewId.ToLong();
    }

    public static ViewKind KindOf(View view)
    {
        return view.IsTemplate
            ? ViewKind.Other
            : view.ViewType switch
            {
                ViewType.FloorPlan or ViewType.EngineeringPlan => ViewKind.Plan,
                ViewType.CeilingPlan => ViewKind.CeilingPlan,
                ViewType.AreaPlan => ViewKind.AreaPlan,
                ViewType.Section or ViewType.Elevation or ViewType.Detail => ViewKind.Section,
                ViewType.ThreeD or ViewType.Walkthrough => ViewKind.ThreeD,
                _ => ViewKind.Other
            };
    }
}
