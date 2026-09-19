using RevitDevTool.Tools.Selection;

namespace RevitDevTool.Tools.Geometry;

/// <summary>
/// Element AABB helpers. Optional <see cref="Transform"/> maps link-space boxes into host space.
/// </summary>
internal static class ElementBox
{
    /// <summary>Union boxes in the document that owns <paramref name="ids"/> (no transform).</summary>
    internal static bool TryBox(
        Document document,
        IEnumerable<ElementId> ids,
        View? view,
        double factor,
        out XYZ min,
        out XYZ max)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(ids);

        return TryBox(ResolveElements(document, ids), transform: null, view, factor, out min, out max);
    }

    /// <summary>
    /// Resolve ids in the link document, then transform the union into host space via
    /// <see cref="RevitLinkInstance.GetTotalTransform"/>.
    /// </summary>
    internal static bool TryBox(
        RevitLinkInstance instance,
        IEnumerable<ElementId> ids,
        View? view,
        double factor,
        out XYZ min,
        out XYZ max)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(ids);

        var linkDoc = instance.GetLinkDocument();
        if (linkDoc is null)
        {
            min = XYZ.Zero;
            max = XYZ.Zero;
            return false;
        }

        return TryBox(
            ResolveElements(linkDoc, ids),
            instance.GetTotalTransform(),
            view,
            factor,
            out min,
            out max);
    }

    /// <summary>Union already-resolved elements; optional <paramref name="transform"/> (e.g. link total).</summary>
    internal static bool TryBox(
        IReadOnlyList<Element> elements,
        Transform? transform,
        View? view,
        double factor,
        out XYZ min,
        out XYZ max)
    {
        ArgumentNullException.ThrowIfNull(elements);

        min = XYZ.Zero;
        max = XYZ.Zero;

        if (!TryUnion(elements, view, out var unionMin, out var unionMax))
            return false;

        if (transform is null)
        {
            Expand(unionMin, unionMax, factor, out min, out max);
            return true;
        }

        if (!TryTransform(unionMin, unionMax, transform, out var transformedMin, out var transformedMax))
            return false;

        Expand(transformedMin, transformedMax, factor, out min, out max);
        return true;
    }

    /// <summary>Convenience: elements already in hand + link instance transform.</summary>
    internal static bool TryBox(
        IReadOnlyList<Element> elements,
        RevitLinkInstance instance,
        View? view,
        double factor,
        out XYZ min,
        out XYZ max)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return TryBox(elements, instance.GetTotalTransform(), view, factor, out min, out max);
    }

    internal static bool TryMatchBox(
        Document document,
        SearchMatch match,
        View? view,
        double factor,
        out XYZ min,
        out XYZ max)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(match);

        min = XYZ.Zero;
        max = XYZ.Zero;

        return match switch
        {
            SearchMatch.HostElements host => TryBox(document, host.Ids, view, factor, out min, out max),
            SearchMatch.LinkedElements linked => TryBox(
                linked.Instance, linked.Ids, view, factor, out min, out max),
            _ => false
        };
    }

    /// <summary>Scale AABB about its center (<paramref name="factor"/> 1.0 = unchanged, 1.25 = +25%).</summary>
    internal static void Expand(XYZ min, XYZ max, double factor, out XYZ expandedMin, out XYZ expandedMax)
    {
        var center = (min + max) * 0.5;
        var half = (max - min) * 0.5 * factor;
        expandedMin = center - half;
        expandedMax = center + half;
    }

    /// <summary>Component-wise AABB union of two boxes.</summary>
    internal static void Union(ref XYZ? min, ref XYZ? max, XYZ otherMin, XYZ otherMax)
    {
        min = min is null ? otherMin : GetMin(min, otherMin);
        max = max is null ? otherMax : GetMax(max, otherMax);
    }

    private static List<Element> ResolveElements(Document document, IEnumerable<ElementId> ids)
    {
        var elements = new List<Element>();
        foreach (var id in ids)
        {
            if (document.GetElement(id) is { } element)
                elements.Add(element);
        }

        return elements;
    }

    private static bool TryUnion(
        IReadOnlyList<Element?> elements,
        View? view,
        out XYZ unionMin,
        out XYZ unionMax)
    {
        unionMin = XYZ.Zero;
        unionMax = XYZ.Zero;

        XYZ? min = null;
        XYZ? max = null;
        foreach (var element in elements)
        {
            if (element is null)
                continue;

            var box = element.get_BoundingBox(view) ?? element.get_BoundingBox(null);
            if (box is null)
                continue;

            min = min is null ? box.Min : GetMin(min, box.Min);
            max = max is null ? box.Max : GetMax(max, box.Max);
        }

        if (min is null || max is null)
            return false;

        unionMin = min;
        unionMax = max;
        return true;
    }

    private static bool TryTransform(
        XYZ min,
        XYZ max,
        Transform transform,
        out XYZ transformedMin,
        out XYZ transformedMax)
    {
        transformedMin = XYZ.Zero;
        transformedMax = XYZ.Zero;

        XYZ? tMin = null;
        XYZ? tMax = null;
        foreach (var corner in Corners(min, max))
        {
            var point = transform.OfPoint(corner);
            tMin = tMin is null ? point : GetMin(tMin, point);
            tMax = tMax is null ? point : GetMax(tMax, point);
        }

        if (tMin is null || tMax is null)
            return false;

        transformedMin = tMin;
        transformedMax = tMax;
        return true;
    }

    private static XYZ[] Corners(XYZ min, XYZ max)
        =>
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

    private static XYZ GetMin(XYZ a, XYZ b)
        => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));

    private static XYZ GetMax(XYZ a, XYZ b)
        => new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
}
