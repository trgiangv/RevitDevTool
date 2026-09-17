namespace RevitDevTool.Logging.Linkify;

internal static class LinkBoundingBox
{
    internal static bool TryGetBoundingBox(
        IReadOnlyList<Element> elements,
        RevitLinkInstance instance,
        Autodesk.Revit.DB.View? activeView,
        double pad,
        out XYZ hostMin,
        out XYZ hostMax)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(instance);

        hostMin = XYZ.Zero;
        hostMax = XYZ.Zero;

        XYZ? unionMin = null;
        XYZ? unionMax = null;
        foreach (var element in elements)
        {
            if (element is null)
                continue;

            var box = element.get_BoundingBox(null) ?? element.get_BoundingBox(activeView);
            if (box is null)
                continue;

            unionMin = unionMin is null ? box.Min : ComponentMin(unionMin, box.Min);
            unionMax = unionMax is null ? box.Max : ComponentMax(unionMax, box.Max);
        }

        if (unionMin is null || unionMax is null)
            return false;

        var transform = instance.GetTotalTransform();
        XYZ? transformedMin = null;
        XYZ? transformedMax = null;
        foreach (var corner in Corners(unionMin, unionMax))
        {
            var hostPoint = transform.OfPoint(corner);
            transformedMin = transformedMin is null ? hostPoint : ComponentMin(transformedMin, hostPoint);
            transformedMax = transformedMax is null ? hostPoint : ComponentMax(transformedMax, hostPoint);
        }

        if (transformedMin is null || transformedMax is null)
            return false;

        var center = (transformedMin + transformedMax) * 0.5;
        var half = (transformedMax - transformedMin) * 0.5 * pad;
        hostMin = center - half;
        hostMax = center + half;
        return true;
    }

    private static XYZ[] Corners(XYZ min, XYZ max)
    {
        return
        [
            new XYZ(min.X, min.Y, min.Z),
            new XYZ(min.X, min.Y, max.Z),
            new XYZ(min.X, max.Y, min.Z),
            new XYZ(min.X, max.Y, max.Z),
            new XYZ(max.X, min.Y, min.Z),
            new XYZ(max.X, min.Y, max.Z),
            new XYZ(max.X, max.Y, min.Z),
            new XYZ(max.X, max.Y, max.Z)
        ];
    }

    private static XYZ ComponentMin(XYZ a, XYZ b)
    {
        return new XYZ(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
    }

    private static XYZ ComponentMax(XYZ a, XYZ b)
    {
        return new XYZ(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
    }
}
