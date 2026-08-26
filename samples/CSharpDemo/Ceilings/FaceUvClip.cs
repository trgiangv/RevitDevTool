#if !REVIT2025_OR_GREATER
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

namespace CSharpDemo.Ceilings;

internal sealed class FaceUvClip
{
    private const double LengthToleranceFeet = 1e-9;
    private const double EnvelopeCoverPadding = 1;
    private const int UvLoopMinPoints = 3;
    private const int ClosedRingMinCoordinates = 4;
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 0);

    private FaceUvClip(NtsGeometry polygon)
    {
        Polygon = polygon;
    }

    private NtsGeometry Polygon { get; }
    public Envelope Envelope => Polygon.EnvelopeInternal;

    public static FaceUvClip? TryCreate(CeilingPlanarFace face)
    {
        var loops = ReadUvLoops(face);
        var polygon = BuildPolygon(loops);
        if (polygon == null || polygon.IsEmpty)
            return null;

        return new FaceUvClip(polygon);
    }

    public IReadOnlyList<UvSegment> Intersect(UV origin, UV direction)
    {
        var (minT, maxT) = ProjectEnvelope(origin, direction);
        var start = origin.Add(direction.Multiply(minT - EnvelopeCoverPadding));
        var end = origin.Add(direction.Multiply(maxT + EnvelopeCoverPadding));
        if (start.DistanceTo(end) <= LengthToleranceFeet)
            return [];

        var line = Factory.CreateLineString(
        [
            new Coordinate(start.U, start.V),
            new Coordinate(end.U, end.V)
        ]);

        var segments = new List<UvSegment>();
        Collect(line.Intersection(Polygon), segments);
        return segments;
    }

    public static UV[] EnvelopeCorners(Envelope envelope) =>
    [
        new UV(envelope.MinX, envelope.MinY),
        new UV(envelope.MinX, envelope.MaxY),
        new UV(envelope.MaxX, envelope.MinY),
        new UV(envelope.MaxX, envelope.MaxY)
    ];

    private (double Min, double Max) ProjectEnvelope(UV origin, UV direction)
    {
        var minT = double.MaxValue;
        var maxT = double.MinValue;
        foreach (var corner in EnvelopeCorners(Envelope))
        {
            var t = corner.Subtract(origin).DotProduct(direction);
            if (t < minT) minT = t;
            if (t > maxT) maxT = t;
        }

        return (minT, maxT);
    }

    private static List<List<UV>> ReadUvLoops(CeilingPlanarFace face)
    {
        var loops = new List<List<UV>>();
        foreach (var loop in face.Face.GetEdgesAsCurveLoops())
        {
            var points = LoopUvPoints(face, loop);
            if (points.Count >= UvLoopMinPoints)
                loops.Add(points);
        }

        return loops;
    }

    private static List<UV> LoopUvPoints(CeilingPlanarFace face, CurveLoop loop)
    {
        var points = new List<UV>();
        foreach (var curve in loop)
        {
            var tess = curve.Tessellate();
            for (var i = 0; i < tess.Count - 1; i++)
                points.Add(face.ToUv(tess[i]));
        }

        return points;
    }

    private static NtsGeometry? BuildPolygon(List<List<UV>> loops)
    {
        var rings = loops
            .Select(TryClosedRing)
            .OfType<LinearRing>()
            .Select(ring => (ring, area: Math.Abs(Factory.CreatePolygon(ring).Area)))
            .OrderByDescending(item => item.area)
            .ToList();
        if (rings.Count == 0)
            return null;

        var shell = AsCcw(rings[0].ring);
        var holes = rings.Skip(1).Select(item => AsCw(item.ring)).ToArray();
        NtsGeometry polygon = Factory.CreatePolygon(shell, holes);
        return polygon.IsValid ? polygon : polygon.Buffer(0);
    }

    private static LinearRing? TryClosedRing(List<UV> points)
    {
        var coords = new List<Coordinate>(points.Count + 1);
        foreach (var point in points)
            coords.Add(new Coordinate(point.U, point.V));
        if (!coords[0].Equals2D(coords[^1]))
            coords.Add(new Coordinate(coords[0].X, coords[0].Y));
        if (coords.Count < ClosedRingMinCoordinates)
            return null;

        return Factory.CreateLinearRing(coords.ToArray());
    }

    private static LinearRing AsCcw(LinearRing ring) =>
        Orientation.IsCCW(ring.CoordinateSequence) ? ring : (LinearRing)ring.Reverse();

    private static LinearRing AsCw(LinearRing ring) =>
        Orientation.IsCCW(ring.CoordinateSequence) ? (LinearRing)ring.Reverse() : ring;

    private static void Collect(NtsGeometry geometry, List<UvSegment> segments)
    {
        switch (geometry)
        {
            case LineString line:
                AddLineString(line, segments);
                break;
            case GeometryCollection collection:
                for (var i = 0; i < collection.NumGeometries; i++)
                    Collect(collection.GetGeometryN(i), segments);
                break;
        }
    }

    private static void AddLineString(LineString line, List<UvSegment> segments)
    {
        if (line.NumPoints < 2)
            return;

        for (var i = 0; i < line.NumPoints - 1; i++)
        {
            var start = line.GetCoordinateN(i);
            var end = line.GetCoordinateN(i + 1);
            if (start.Distance(end) <= LengthToleranceFeet)
                continue;

            segments.Add(new UvSegment(new UV(start.X, start.Y), new UV(end.X, end.Y)));
        }
    }
}
#endif
