#if !REVIT2025_OR_GREATER
using System.Diagnostics;
namespace CSharpDemo.Ceilings;

internal static class CeilingGridLines
{
    public static IList<Curve> Get(Ceiling ceiling, bool includeBoundary) =>
        GetCore(ceiling, includeBoundary, link: null);

    public static IList<Curve> Get(Ceiling ceiling, bool includeBoundary, RevitLinkInstance link) =>
        GetCore(ceiling, includeBoundary, link);

    private static IList<Curve> GetCore(Ceiling ceiling, bool includeBoundary, RevitLinkInstance? link)
    {
        var curves = new List<Curve>();
        var bottomFaceRef = HostObjectUtils.GetBottomFaces(ceiling).FirstOrDefault();
        if (bottomFaceRef == null)
            return curves;
        if (ceiling.GetGeometryObjectFromReference(bottomFaceRef) is not PlanarFace face)
            return curves;

        var toHost = link?.GetTotalTransform() ?? Transform.Identity;
        var mappedFace = new CeilingPlanarFace(face, toHost);
        if (includeBoundary)
            curves.AddRange(mappedFace.GetBoundary());

        var pattern = TryGetModelFillPattern(ceiling, face);
        if (pattern == null)
            return curves;

        var grids = link is null
            ? HatchGrid.Read(ceiling, bottomFaceRef, mappedFace, pattern)
            : HatchGrid.Read(link, ceiling, bottomFaceRef, mappedFace, pattern);
        if (grids.Count == 0 || FaceUvClip.TryCreate(mappedFace) is not { } clip)
            return curves;

        curves.AddRange(HatchGrid.PaintAll(grids, mappedFace, clip, ceiling.Document.Application.ShortCurveTolerance));
        return curves;
    }

    private static FillPattern? TryGetModelFillPattern(Ceiling ceiling, PlanarFace face)
    {
        var doc = ceiling.Document;
        foreach (var id in MaterialIds(ceiling, face))
        {
            if (doc.GetElement(id) is not Material material)
                continue;
            if (TryModelPattern(material, doc) is { } pattern)
                return pattern;
        }

        return null;
    }

    private static FillPattern? TryModelPattern(Material material, Document doc)
    {
        foreach (var patternId in new[] { material.SurfaceForegroundPatternId, material.SurfaceBackgroundPatternId })
        {
            if (patternId == ElementId.InvalidElementId)
                continue;
            if (doc.GetElement(patternId) is not FillPatternElement patternElement)
                continue;

            var pattern = patternElement.GetFillPattern();
            if (pattern.IsSolidFill || pattern.Target != FillPatternTarget.Model)
                continue;

            Debug.WriteLine($"pattern={patternElement.Name} grids={pattern.GridCount} material={material.Name}");
            return pattern;
        }

        return null;
    }

    private static IEnumerable<ElementId> MaterialIds(Ceiling ceiling, PlanarFace face)
    {
        if (face.MaterialElementId != ElementId.InvalidElementId)
            yield return face.MaterialElementId;

        foreach (var id in ceiling.GetMaterialIds(false))
        {
            if (id != ElementId.InvalidElementId)
                yield return id;
        }

        if (ceiling.Document.GetElement(ceiling.GetTypeId()) is not CeilingType ceilingType)
            yield break;

        var structure = ceilingType.GetCompoundStructure();
        if (structure == null)
            yield break;

        foreach (var layer in structure.GetLayers())
        {
            if (layer.MaterialId != ElementId.InvalidElementId)
                yield return layer.MaterialId;
        }
    }
}
#endif
