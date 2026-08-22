namespace DevTools.TUnit.SampleTests;

// Scope: [ClassDataSource<T>] where T carries Revit API instances built from scalar seeds.

public sealed class RevitBoxCase
{
    public double MinX { get; set; } = 0;
    public double MinY { get; set; } = 0;
    public double MinZ { get; set; } = 0;
    public double MaxX { get; set; } = 1;
    public double MaxY { get; set; } = 1;
    public double MaxZ { get; set; } = 1;
    public string Label { get; set; } = "unit";

    public BoundingBoxXYZ ToBox() =>
        RevitSampleGeometry.Box(MinX, MinY, MinZ, MaxX, MaxY, MaxZ);
}

[ClassDataSource<RevitBoxCase>]
public sealed class ClassDataSourceRevitTypeTests(RevitBoxCase data)
{
    [Test]
    public async Task Class_data_builds_positive_revit_box()
    {
        var box = data.ToBox();
        await Assert.That(data.Label).IsNotEmpty();
        await Assert.That(box.Max.X - box.Min.X).IsGreaterThan(0.0);
        await Assert.That(box.Max.Y - box.Min.Y).IsGreaterThan(0.0);
        await Assert.That(box.Max.Z - box.Min.Z).IsGreaterThan(0.0);
    }
}
