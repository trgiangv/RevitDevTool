namespace DevTools.TUnit.SampleTests;

// Scope: Revit BoundingBoxXYZ geometry, outline, and extrusion smoke tests.

public sealed class RevitBoundingBoxGeometryTests
{
    static readonly (double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)[] Spans =
    [
        (-12.3, 45.6, -7.8, 34.5, 67.8, 12.3),
        (-99.9, -88.8, -10.5, -10.4, -20.3, 5.7),
        (23.4, -56.7, 11.1, 89.9, 22.2, 77.7),
        (-45.5, 12.4, 33.3, -12.1, 98.7, 66.6),
        (10.5, 20.5, 30.5, 40.5, 50.5, 60.5),
    ];

    static readonly (double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ, double Offset)[] SpansWithOffset =
    [
        (-12.3, 45.6, -7.8, 34.5, 67.8, 12.3, 1.2),
        (-99.9, -88.8, -10.5, -10.4, -20.3, 5.7, 3.4),
        (23.4, -56.7, 11.1, 89.9, 22.2, 77.7, 2.1),
        (-45.5, 12.4, 33.3, -12.1, 98.7, 66.6, 4.5),
        (10.5, 20.5, 30.5, 40.5, 50.5, 60.5, 5.5),
    ];

    [Test]
    public async Task Volume_from_extruded_bottom()
    {
        foreach (var span in Spans)
        {
            var solid = CreateSolidFromBoundingBox(RevitSampleGeometry.Box(
                span.MinX, span.MinY, span.MinZ, span.MaxX, span.MaxY, span.MaxZ));
            await Assert.That(solid).IsNotNull();
            await Assert.That(solid.Volume).IsGreaterThan(0.0);
        }
    }

    [Test]
    public async Task Bottom_corners_share_min_z()
    {
        foreach (var span in Spans)
        {
            var pts = BottomCorners(RevitSampleGeometry.Box(
                span.MinX, span.MinY, span.MinZ, span.MaxX, span.MaxY, span.MaxZ));
            await Assert.That(pts.Count).IsEqualTo(4);
            foreach (var point in pts)
                await Assert.That(point.Z).IsEqualTo(span.MinZ).Within(1e-9);
        }
    }

    [Test]
    public async Task Bottom_corners_axis_aligned_order()
    {
        var pts = BottomCorners(RevitSampleGeometry.Box(10, 5, 0, 13, 7, 3));
        await Assert.That(pts[0].IsAlmostEqualTo(new XYZ(10, 5, 0))).IsTrue();
        await Assert.That(pts[1].IsAlmostEqualTo(new XYZ(13, 5, 0))).IsTrue();
        await Assert.That(pts[2].IsAlmostEqualTo(new XYZ(13, 7, 0))).IsTrue();
        await Assert.That(pts[3].IsAlmostEqualTo(new XYZ(10, 7, 0))).IsTrue();
    }

    [Test]
    public async Task Outline_matches_min_max()
    {
        foreach (var span in Spans)
        {
            var bb = RevitSampleGeometry.Box(span.MinX, span.MinY, span.MinZ, span.MaxX, span.MaxY, span.MaxZ);
            var outline = new Outline(bb.Min, bb.Max);
            await Assert.That(outline.MinimumPoint.X).IsEqualTo(span.MinX).Within(1e-9);
            await Assert.That(outline.MaximumPoint.X).IsEqualTo(span.MaxX).Within(1e-9);
            await Assert.That(outline.MinimumPoint.Z).IsEqualTo(span.MinZ).Within(1e-9);
        }
    }

    [Test]
    public async Task Expand_offset_grows_all_axes()
    {
        foreach (var span in SpansWithOffset)
        {
            var bb = RevitSampleGeometry.Box(span.MinX, span.MinY, span.MinZ, span.MaxX, span.MaxY, span.MaxZ);
            var beforeMin = bb.Min;
            var beforeMax = bb.Max;
            bb.Min = new XYZ(beforeMin.X - span.Offset, beforeMin.Y - span.Offset, beforeMin.Z - span.Offset);
            bb.Max = new XYZ(beforeMax.X + span.Offset, beforeMax.Y + span.Offset, beforeMax.Z + span.Offset);
            await Assert.That(bb.Min.X).IsLessThan(beforeMin.X);
            await Assert.That(bb.Min.Y).IsLessThan(beforeMin.Y);
            await Assert.That(bb.Min.Z).IsLessThan(beforeMin.Z);
            await Assert.That(bb.Max.X).IsGreaterThan(beforeMax.X);
            await Assert.That(bb.Max.Y).IsGreaterThan(beforeMax.Y);
            await Assert.That(bb.Max.Z).IsGreaterThan(beforeMax.Z);
        }
    }

    [Test]
    public async Task Length_width_height_and_center()
    {
        foreach (var span in Spans)
        {
            var bb = RevitSampleGeometry.Box(span.MinX, span.MinY, span.MinZ, span.MaxX, span.MaxY, span.MaxZ);
            await Assert.That(bb.Max.X - bb.Min.X).IsGreaterThanOrEqualTo(0.0);
            await Assert.That(bb.Max.Y - bb.Min.Y).IsGreaterThanOrEqualTo(0.0);
            await Assert.That(bb.Max.Z - bb.Min.Z).IsGreaterThanOrEqualTo(0.0);
            var center = (bb.Min + bb.Max) * 0.5;
            await Assert.That(center.X).IsEqualTo((span.MaxX + span.MinX) / 2.0).Within(1e-9);
        }
    }

    static IReadOnlyList<XYZ> BottomCorners(BoundingBoxXYZ bb)
    {
        var min = bb.Min;
        var max = bb.Max;
        return
        [
            new XYZ(min.X, min.Y, min.Z),
            new XYZ(max.X, min.Y, min.Z),
            new XYZ(max.X, max.Y, min.Z),
            new XYZ(min.X, max.Y, min.Z),
        ];
    }

    static Solid CreateSolidFromBoundingBox(BoundingBoxXYZ bb)
    {
        var min = bb.Min;
        var max = bb.Max;
        var loop = CurveLoop.Create(
        [
            Line.CreateBound(new XYZ(min.X, min.Y, min.Z), new XYZ(max.X, min.Y, min.Z)),
            Line.CreateBound(new XYZ(max.X, min.Y, min.Z), new XYZ(max.X, max.Y, min.Z)),
            Line.CreateBound(new XYZ(max.X, max.Y, min.Z), new XYZ(min.X, max.Y, min.Z)),
            Line.CreateBound(new XYZ(min.X, max.Y, min.Z), new XYZ(min.X, min.Y, min.Z)),
        ]);
        return GeometryCreationUtilities.CreateExtrusionGeometry(
            [loop],
            XYZ.BasisZ,
            Math.Abs(max.Z - min.Z));
    }
}
