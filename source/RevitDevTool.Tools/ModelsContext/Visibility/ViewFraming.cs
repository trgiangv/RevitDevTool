using DevTools.ModelsContext.Documents;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.Geometry;
using RevitDevTool.Tools.ModelsContext.Documents;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal static class ViewFraming
{
    public static bool TryFrame(
        Document model,
        View view,
        IReadOnlyCollection<ElementRef> elements,
        double factor,
        out XYZ min,
        out XYZ max)
    {
        XYZ? unionMin = null;
        XYZ? unionMax = null;
        foreach (var group in DocumentRegistry.GroupByDocument(model, elements))
        {
            if (TryBoxGroup(model, view, group, out var boxMin, out var boxMax))
            {
                ElementBox.Union(ref unionMin, ref unionMax, boxMin, boxMax);
            }
        }

        if (unionMin is null || unionMax is null)
        {
            min = XYZ.Zero;
            max = XYZ.Zero;
            return false;
        }

        ElementBox.Expand(unionMin, unionMax, factor, out min, out max);
        return true;
    }

    private static bool TryBoxGroup(
        Document model,
        View view,
        IGrouping<DocumentKey, ElementRef> group,
        out XYZ min,
        out XYZ max)
    {
        var ids = group.Select(element => element.Id.ToElementId()).ToList();
        if (group.Key.IsCurrentModel)
        {
            return ElementBox.TryBox(model, ids, view, 1.0, out min, out max);
        }

        if (model.GetElement(group.Key.LinkInstanceId.ToElementId()) is RevitLinkInstance instance)
        {
            return ElementBox.TryBox(instance, ids, view, 1.0, out min, out max);
        }

        min = XYZ.Zero;
        max = XYZ.Zero;
        return false;
    }
}
