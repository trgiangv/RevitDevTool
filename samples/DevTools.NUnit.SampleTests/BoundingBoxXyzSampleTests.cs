using Autodesk.Revit.DB;
using NUnit.Framework;

namespace DevTools.NUnit.SampleTests;

// Discover: [TestCaseSource] of primitives. BoundingBoxXYZ is built in the test body.

[TestFixture]
public sealed class BoundingBoxXyzSampleTests
{
    public static IEnumerable<TestCaseData> Spans()
    {
        yield return new TestCaseData(-12.3, 45.6, -7.8, 34.5, 67.8, 12.3);
        yield return new TestCaseData(-99.9, -88.8, -10.5, -10.4, -20.3, 5.7);
        yield return new TestCaseData(23.4, -56.7, 11.1, 89.9, 22.2, 77.7);
        yield return new TestCaseData(-45.5, 12.4, 33.3, -12.1, 98.7, 66.6);
        yield return new TestCaseData(10.5, 20.5, 30.5, 40.5, 50.5, 60.5);
    }

    public static IEnumerable<TestCaseData> SpansWithOffset()
    {
        yield return new TestCaseData(-12.3, 45.6, -7.8, 34.5, 67.8, 12.3, 1.2);
        yield return new TestCaseData(-99.9, -88.8, -10.5, -10.4, -20.3, 5.7, 3.4);
        yield return new TestCaseData(23.4, -56.7, 11.1, 89.9, 22.2, 77.7, 2.1);
        yield return new TestCaseData(-45.5, 12.4, 33.3, -12.1, 98.7, 66.6, 4.5);
        yield return new TestCaseData(10.5, 20.5, 30.5, 40.5, 50.5, 60.5, 5.5);
    }

    [TestCaseSource(nameof(Spans))]
    public void Volume_from_extruded_bottom(
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        var solid = CreateSolidFromBoundingBox(Box(minX, minY, minZ, maxX, maxY, maxZ));
        Assert.That(solid, Is.Not.Null);
        Assert.That(solid.Volume, Is.GreaterThan(0.0));
    }

    [TestCaseSource(nameof(Spans))]
    public void Bottom_corners_share_min_z(
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        var pts = BottomCorners(Box(minX, minY, minZ, maxX, maxY, maxZ));
        Assert.That(pts, Has.Count.EqualTo(4));
        foreach (var p in pts)
            Assert.That(p.Z, Is.EqualTo(minZ).Within(1e-9));
    }

    [Test]
    public void Bottom_corners_axis_aligned_order()
    {
        var pts = BottomCorners(Box(10, 5, 0, 13, 7, 3));
        Assert.That(pts[0].IsAlmostEqualTo(new XYZ(10, 5, 0)), Is.True);
        Assert.That(pts[1].IsAlmostEqualTo(new XYZ(13, 5, 0)), Is.True);
        Assert.That(pts[2].IsAlmostEqualTo(new XYZ(13, 7, 0)), Is.True);
        Assert.That(pts[3].IsAlmostEqualTo(new XYZ(10, 7, 0)), Is.True);
    }

    [TestCaseSource(nameof(Spans))]
    public void Outline_matches_min_max(
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        var bb = Box(minX, minY, minZ, maxX, maxY, maxZ);
        var outline = new Outline(bb.Min, bb.Max);
        Assert.That(outline.MinimumPoint.X, Is.EqualTo(minX).Within(1e-9));
        Assert.That(outline.MaximumPoint.X, Is.EqualTo(maxX).Within(1e-9));
        Assert.That(outline.MinimumPoint.Z, Is.EqualTo(minZ).Within(1e-9));
    }

    [TestCaseSource(nameof(SpansWithOffset))]
    public void Expand_offset_grows_all_axes(
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ, double offset)
    {
        var bb = Box(minX, minY, minZ, maxX, maxY, maxZ);
        var beforeMin = bb.Min;
        var beforeMax = bb.Max;
        bb.Min = new XYZ(beforeMin.X - offset, beforeMin.Y - offset, beforeMin.Z - offset);
        bb.Max = new XYZ(beforeMax.X + offset, beforeMax.Y + offset, beforeMax.Z + offset);
        Assert.That(bb.Min.X, Is.LessThan(beforeMin.X));
        Assert.That(bb.Min.Y, Is.LessThan(beforeMin.Y));
        Assert.That(bb.Min.Z, Is.LessThan(beforeMin.Z));
        Assert.That(bb.Max.X, Is.GreaterThan(beforeMax.X));
        Assert.That(bb.Max.Y, Is.GreaterThan(beforeMax.Y));
        Assert.That(bb.Max.Z, Is.GreaterThan(beforeMax.Z));
    }

    [TestCaseSource(nameof(Spans))]
    public void Length_width_height_and_center(
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        var bb = Box(minX, minY, minZ, maxX, maxY, maxZ);
        Assert.That(bb.Max.X - bb.Min.X, Is.GreaterThanOrEqualTo(0.0));
        Assert.That(bb.Max.Y - bb.Min.Y, Is.GreaterThanOrEqualTo(0.0));
        Assert.That(bb.Max.Z - bb.Min.Z, Is.GreaterThanOrEqualTo(0.0));
        var center = (bb.Min + bb.Max) * 0.5;
        Assert.That(center.X, Is.EqualTo((maxX + minX) / 2.0).Within(1e-9));
    }

    internal static BoundingBoxXYZ Box(
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ) =>
        new()
        {
            Min = new XYZ(minX, minY, minZ),
            Max = new XYZ(maxX, maxY, maxZ),
            Transform = Transform.Identity,
        };

    private static IReadOnlyList<XYZ> BottomCorners(BoundingBoxXYZ bb)
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

    private static Solid CreateSolidFromBoundingBox(BoundingBoxXYZ bb)
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
