#if !REVIT2025_OR_GREATER
using System.Diagnostics;
namespace CSharpDemo.Ceilings;

internal readonly record struct CeilingPlanarFace(PlanarFace Face, Transform ToHost)
{
    public XYZ HostNormal => ToHost.OfVector(Face.FaceNormal).Normalize();

    public UV ToUv(XYZ linkPoint)
    {
        var vector = linkPoint - Face.Origin;
        return new UV(vector.DotProduct(Face.XVector), vector.DotProduct(Face.YVector));
    }

    public UV ToUvFromHost(XYZ hostPoint) => ToUv(ToHost.Inverse.OfPoint(hostPoint));

    public UV ToUvDirFromHost(XYZ hostDir)
    {
        var linkDir = ToHost.Inverse.OfVector(hostDir);
        return new UV(linkDir.DotProduct(Face.XVector), linkDir.DotProduct(Face.YVector));
    }

    public XYZ ToHostPoint(UV uv) => ToHost.OfPoint(Face.Evaluate(uv));

    public List<Curve> GetBoundary()
    {
        var toHost = ToHost;
        return Face.GetEdgesAsCurveLoops()
            .SelectMany(loop => loop)
            .Select(curve => curve.CreateTransformed(toHost))
            .ToList();
    }

    public Line? TryHostLine(UvSegment segment, double minLength)
    {
        var start = ToHostPoint(segment.A);
        var end = ToHostPoint(segment.B);
        return start.DistanceTo(end) <= minLength ? null : Line.CreateBound(start, end);
    }

    public XYZ ToHostVector(UV uvDir) =>
        ToHost.OfVector(Face.XVector.Multiply(uvDir.U) + Face.YVector.Multiply(uvDir.V));
}

internal readonly record struct UvSegment(UV A, UV B);

internal readonly record struct HatchGrid(
    XYZ Origin,
    XYZ Along,
    XYZ Step,
    double Spacing,
    IList<double> Segments)
{
    private const double LengthToleranceFeet = 1e-9;
    private const double UvLengthTolerance = 1e-12;
    private const double DirectionParallelTolerance = 0.15;
    private const double HatchDimensionMoveFeet = 0.1;
    private const double DummyDimensionLengthFeet = 10;
    private const int CeilingHatchBaseOffset = 1;
    private const int FloorHatchBaseOffset = 2;
    private const int HatchLineIndexPadding = 1;
    private const int MaxHatchLinesPerGrid = 4000;
    private const int StableRepresentationSuffixLength = 10;
    private const double HatchOriginHalfSpacing = 0.5;

    public static IReadOnlyList<HatchGrid> Read(
        Ceiling ceiling,
        Reference bottomFaceRef,
        CeilingPlanarFace face,
        FillPattern pattern)
    {
        var ceilingDoc = ceiling.Document;
        var stableFace = bottomFaceRef.ConvertToStableRepresentation(ceilingDoc);
        Debug.WriteLine($"hatch face stable: {stableFace}");
        return ReadFromStable(ceilingDoc, stableFace, pattern, face);
    }

    public static IReadOnlyList<HatchGrid> Read(
        RevitLinkInstance link,
        Ceiling ceiling,
        Reference bottomFaceRef,
        CeilingPlanarFace face,
        FillPattern pattern)
    {
        var hostDoc = link.Document;
        var stableFace = bottomFaceRef.CreateLinkReference(link).ConvertToStableRepresentation(hostDoc);
        Debug.WriteLine($"hatch face stable: {stableFace}");

        var grids = ReadFromStable(hostDoc, stableFace, pattern, face);
        if (grids.Count > 0)
            return grids;

        var linkStable = bottomFaceRef.ConvertToStableRepresentation(ceiling.Document);
        var tail = linkStable.Length >= StableRepresentationSuffixLength
            ? linkStable[^StableRepresentationSuffixLength..]
            : linkStable;
        stableFace = $"{link.UniqueId}:0:RVTLINK:{ceiling.Id}{tail}";
        Debug.WriteLine($"hatch face stable fallback: {stableFace}");
        return ReadFromStable(hostDoc, stableFace, pattern, face);
    }

    public static List<Curve> PaintAll(
        IReadOnlyList<HatchGrid> grids,
        CeilingPlanarFace face,
        FaceUvClip clip,
        double shortCurveTolerance)
    {
        var curves = new List<Curve>();
        foreach (var grid in grids)
            curves.AddRange(grid.Paint(face, clip, shortCurveTolerance));
        return curves;
    }

    private List<Curve> Paint(CeilingPlanarFace face, FaceUvClip clip, double shortCurveTolerance)
    {
        var curves = new List<Curve>();
        if (TryUvAxes(face) is not { } uv)
            return curves;

        var (kMin, kMax) = LineRange(uv.Origin, uv.Step, clip.Envelope);
        for (var k = kMin; k <= kMax; k++)
            curves.AddRange(PaintLine(uv.Origin.Add(uv.Step.Multiply(k * Spacing)), uv, face, clip, shortCurveTolerance));

        Debug.WriteLine($"grid clip k={kMin}..{kMax} segments={curves.Count} uvOrigin=({uv.Origin.U:F2},{uv.Origin.V:F2})");
        return curves;
    }

    private (UV Origin, UV Along, UV Step)? TryUvAxes(CeilingPlanarFace face)
    {
        if (Spacing < LengthToleranceFeet)
            return null;

        var along = face.ToUvDirFromHost(Along);
        var step = face.ToUvDirFromHost(Step);
        if (along.IsZeroLength() || step.IsZeroLength())
        {
            Debug.WriteLine("skip grid: UV frame is degenerate");
            return null;
        }

        return (face.ToUvFromHost(Origin), along.Normalize(), step.Normalize());
    }

    private List<Curve> PaintLine(
        UV lineOrigin,
        (UV Origin, UV Along, UV Step) uv,
        CeilingPlanarFace face,
        FaceUvClip clip,
        double shortCurveTolerance)
    {
        var curves = new List<Curve>();
        foreach (var span in clip.Intersect(lineOrigin, uv.Along))
        {
            foreach (var dash in CutDashes(span, uv.Origin, uv.Along, shortCurveTolerance))
            {
                if (face.TryHostLine(dash, shortCurveTolerance) is { } curve)
                    curves.Add(curve);
            }
        }

        return curves;
    }

    private static IReadOnlyList<HatchGrid> ReadFromStable(
        Document hostDoc,
        string stableFace,
        FillPattern pattern,
        CeilingPlanarFace face)
    {
        var grids = new List<HatchGrid>();
        if (pattern.GridCount <= 0)
            return grids;

        using var tx = new Transaction(hostDoc, "Read hatch stable refs");
        tx.Start();
        try
        {
            for (var hatchIndex = 0; hatchIndex < pattern.GridCount; hatchIndex++)
            {
                if (!TryProbe(hostDoc, stableFace, hatchIndex, pattern.GridCount, face.HostNormal, out var grid))
                    continue;

                var origin = face.ToHostPoint(face.ToUvFromHost(grid.Origin));
                grids.Add(grid with
                {
                    Origin = origin,
                    Segments = MatchSegments(pattern, face, grid.Along)
                });
                Debug.WriteLine($"hatch[{hatchIndex}] origin={origin} along={grid.Along} spacing={grid.Spacing}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"hatch stable read failed: {ex.Message}");
        }
        finally
        {
            if (tx.HasStarted())
                tx.RollBack();
        }

        return grids;
    }

    private static bool TryProbe(
        Document hostDoc,
        string stableFace,
        int hatchIndex,
        int gridCount,
        XYZ hostNormal,
        out HatchGrid grid)
    {
        grid = default;
        foreach (var baseOffset in new[] { CeilingHatchBaseOffset, FloorHatchBaseOffset })
        {
            if (!TryHatchRefs(hostDoc, stableFace, hatchIndex, gridCount, baseOffset, out var refs))
                continue;
            if (!TryMeasure(hostDoc, refs, hostNormal, out grid))
                continue;
            return true;
        }

        return false;
    }

    private static bool TryHatchRefs(
        Document hostDoc,
        string stableFace,
        int hatchIndex,
        int gridCount,
        int baseOffset,
        out ReferenceArray refs)
    {
        refs = new ReferenceArray();
        var baseIndex = hatchIndex + baseOffset;
        for (var ip = 0; ip < 2; ip++)
        {
            var hatchStable = $"{stableFace}/{baseIndex + ip * gridCount * 2}";
            try
            {
                refs.Append(Reference.ParseFromStableRepresentation(hostDoc, hatchStable));
                Debug.WriteLine($"parsed {hatchStable}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"skip {hatchStable}: {ex.Message}");
            }
        }

        return refs.Size >= 2;
    }

    private static bool TryMeasure(Document hostDoc, ReferenceArray refs, XYZ hostNormal, out HatchGrid grid)
    {
        grid = default;
        try
        {
            var dimension = hostDoc.Create.NewDimension(
                hostDoc.ActiveView,
                Line.CreateBound(XYZ.Zero, new XYZ(DummyDimensionLengthFeet, 0, 0)),
                refs);
            ElementTransformUtils.MoveElement(hostDoc, dimension.Id, new XYZ(HatchDimensionMoveFeet, 0, 0));

            if (dimension.Value is not { } raw || Math.Abs(raw) < LengthToleranceFeet)
                return false;
            if (dimension.Curve is not Line dimLine)
                return false;

            var step = dimLine.Direction.Normalize();
            var spacing = Math.Abs(raw);
            grid = new HatchGrid(
                dimension.Origin.Subtract(step.Multiply(spacing * HatchOriginHalfSpacing)),
                step.CrossProduct(hostNormal).Normalize(),
                step,
                spacing,
                []);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"dimension failed: {ex.Message}");
            return false;
        }
    }

    private static IList<double> MatchSegments(FillPattern pattern, CeilingPlanarFace face, XYZ along)
    {
        foreach (var fillGrid in pattern.GetFillGrids())
        {
            var uvDir = fillGrid.GetSegmentDirection();
            if (uvDir.IsZeroLength())
                continue;
            var xyzDir = face.ToHostVector(uvDir);
            if (xyzDir.IsZeroLength())
                continue;
            if (xyzDir.Normalize().CrossProduct(along).GetLength() < DirectionParallelTolerance)
                return fillGrid.GetSegments();
        }

        return [];
    }

    private (int Min, int Max) LineRange(UV origin, UV step, NetTopologySuite.Geometries.Envelope envelope)
    {
        var minProj = double.MaxValue;
        var maxProj = double.MinValue;
        foreach (var corner in FaceUvClip.EnvelopeCorners(envelope))
        {
            var proj = corner.Subtract(origin).DotProduct(step);
            if (proj < minProj) minProj = proj;
            if (proj > maxProj) maxProj = proj;
        }

        var kMin = (int)Math.Floor(minProj / Spacing) - HatchLineIndexPadding;
        var kMax = (int)Math.Ceiling(maxProj / Spacing) + HatchLineIndexPadding;
        if (kMax - kMin > MaxHatchLinesPerGrid)
            kMax = kMin + MaxHatchLinesPerGrid;
        return (kMin, kMax);
    }

    private IEnumerable<UvSegment> CutDashes(UvSegment span, UV origin, UV along, double minLength)
    {
        var period = Segments.Sum(Math.Abs);
        if (Segments.Count == 0 || period < UvLengthTolerance)
            return [span];

        var (t0, t1) = OrderedAlong(span, origin, along);
        return WalkDashes(t0, t1, period, origin, along, minLength);
    }

    private static (double T0, double T1) OrderedAlong(UvSegment span, UV origin, UV along)
    {
        var t0 = span.A.Subtract(origin).DotProduct(along);
        var t1 = span.B.Subtract(origin).DotProduct(along);
        return t1 < t0 ? (t1, t0) : (t0, t1);
    }

    private IEnumerable<UvSegment> WalkDashes(
        double t0, double t1, double period, UV origin, UV along, double minLength)
    {
        var cursor = Math.Floor(t0 / period) * period;
        while (cursor < t1)
        {
            foreach (var length in Segments)
            {
                var next = cursor + Math.Abs(length);
                if (PaintedDash(length, cursor, next, t0, t1, origin, along, minLength) is { } dash)
                    yield return dash;

                cursor = next;
                if (cursor >= t1)
                    yield break;
            }
        }
    }

    private static UvSegment? PaintedDash(
        double length, double cursor, double next, double t0, double t1, UV origin, UV along, double minLength)
    {
        if (length <= 0)
            return null;

        var startT = Math.Max(cursor, t0);
        var endT = Math.Min(next, t1);
        if (endT - startT <= minLength)
            return null;

        return new UvSegment(origin.Add(along.Multiply(startT)), origin.Add(along.Multiply(endT)));
    }
}
#endif