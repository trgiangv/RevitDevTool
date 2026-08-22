using Autodesk.Revit.DB;

namespace DevTools.TUnit.SampleTests;

// Scope: [MethodDataSource] rows for Revit geometry.
// Scalar rows expand at testhost; Revit instances are built in the test body.
// (TUnit source-gen cannot materialize BoundingBoxXYZ at testhost — native
// RevitAPI load — mirroring NUnit NotRunnable deferred leaves.)

public sealed class MethodDataSourceRevitTypeTests
{
    [Test]
    [MethodDataSource(nameof(ScalarSpanCases))]
    public async Task Scalar_span_source_builds_positive_box(
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        var box = RevitSampleGeometry.Box(minX, minY, minZ, maxX, maxY, maxZ);
        await Assert.That(box.Max.X - box.Min.X).IsGreaterThan(0.0);
        await Assert.That(box.Max.Y - box.Min.Y).IsGreaterThan(0.0);
        await Assert.That(box.Max.Z - box.Min.Z).IsGreaterThan(0.0);
    }

    public static IEnumerable<(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)> ScalarSpanCases()
    {
        yield return (-12.3, 45.6, -7.8, 34.5, 67.8, 12.3);
        yield return (10.5, 20.5, 30.5, 40.5, 50.5, 60.5);
    }

    [Test]
    [MethodDataSource(nameof(NamedScalarSpanCases))]
    public async Task Named_scalar_span_has_label(
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ, string label)
    {
        var box = RevitSampleGeometry.Box(minX, minY, minZ, maxX, maxY, maxZ);
        await Assert.That(label).IsNotEmpty();
        await Assert.That(box.Max.X - box.Min.X).IsGreaterThan(0.0);
    }

    public static IEnumerable<(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ, string Label)> NamedScalarSpanCases()
    {
        yield return (-12.3, 45.6, -7.8, 34.5, 67.8, 12.3, "Wide_box");
        yield return (10.5, 20.5, 30.5, 40.5, 50.5, 60.5, "Positive_octant");
    }
}
