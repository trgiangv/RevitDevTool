namespace DevTools.TUnit.SampleTests;

// Scope: primary-constructor [Arguments] rows that build Revit BoundingBoxXYZ fixtures.

[Arguments(0, 0, 0, 1, 1, 1, DisplayName = "unit")]
[Arguments(-1, -1, -1, 0, 0, 0, DisplayName = "neg")]
public sealed class RevitTypeConstructorFixtureTests(
    double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
{
    [Test]
    public async Task Span_is_one_on_each_axis()
    {
        var box = RevitSampleGeometry.Box(minX, minY, minZ, maxX, maxY, maxZ);
        await Assert.That(box.Max.X - box.Min.X).IsEqualTo(1.0).Within(1e-9);
        await Assert.That(box.Max.Y - box.Min.Y).IsEqualTo(1.0).Within(1e-9);
        await Assert.That(box.Max.Z - box.Min.Z).IsEqualTo(1.0).Within(1e-9);
    }
}
