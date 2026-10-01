namespace DevTools.MSTest.SampleTests;

// Revit geometry built in the test body. Each case is its own method so the
// discovered id stays Namespace.Type.Method.

[TestClass]
public sealed class BoundingBoxTests
{
    [TestMethod]
    public void Volume_from_extruded_bottom_is_positive()
    {
        var solid = CreateSolid(Box(-12.3, 45.6, -7.8, 34.5, 67.8, 12.3));
        Assert.IsNotNull(solid);
        Assert.IsGreaterThan(0.0, solid.Volume);
    }

    [TestMethod]
    public void Bottom_corners_share_min_z()
    {
        var box = Box(10.5, 20.5, 30.5, 40.5, 50.5, 60.5);
        foreach (var corner in BottomCorners(box))
            Assert.AreEqual(box.Min.Z, corner.Z, 1e-9);
    }

    [TestMethod]
    public void Expand_offset_grows_all_axes()
    {
        const double offset = 1.2;
        var box = Box(-12.3, 45.6, -7.8, 34.5, 67.8, 12.3);
        var beforeMin = box.Min;
        var beforeMax = box.Max;
        box.Min = new XYZ(beforeMin.X - offset, beforeMin.Y - offset, beforeMin.Z - offset);
        box.Max = new XYZ(beforeMax.X + offset, beforeMax.Y + offset, beforeMax.Z + offset);
        Assert.IsTrue(box.Min.X < beforeMin.X);
        Assert.IsTrue(box.Min.Y < beforeMin.Y);
        Assert.IsTrue(box.Min.Z < beforeMin.Z);
        Assert.IsTrue(box.Max.X > beforeMax.X);
        Assert.IsTrue(box.Max.Y > beforeMax.Y);
        Assert.IsTrue(box.Max.Z > beforeMax.Z);
    }

    private static BoundingBoxXYZ Box(
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ) =>
        new()
        {
            Min = new XYZ(minX, minY, minZ),
            Max = new XYZ(maxX, maxY, maxZ),
            Transform = Transform.Identity,
        };

    private static XYZ[] BottomCorners(BoundingBoxXYZ box)
    {
        var min = box.Min;
        var max = box.Max;
        return
        [
            new XYZ(min.X, min.Y, min.Z),
            new XYZ(max.X, min.Y, min.Z),
            new XYZ(max.X, max.Y, min.Z),
            new XYZ(min.X, max.Y, min.Z),
        ];
    }

    private static Solid CreateSolid(BoundingBoxXYZ box)
    {
        var min = box.Min;
        var max = box.Max;
        var loop = CurveLoop.Create(
        [
            Line.CreateBound(new XYZ(min.X, min.Y, min.Z), new XYZ(max.X, min.Y, min.Z)),
            Line.CreateBound(new XYZ(max.X, min.Y, min.Z), new XYZ(max.X, max.Y, min.Z)),
            Line.CreateBound(new XYZ(max.X, max.Y, min.Z), new XYZ(min.X, max.Y, min.Z)),
            Line.CreateBound(new XYZ(min.X, max.Y, min.Z), new XYZ(min.X, min.Y, min.Z)),
        ]);
        return GeometryCreationUtilities.CreateExtrusionGeometry(
            [loop], XYZ.BasisZ, Math.Abs(max.Z - min.Z));
    }
}
