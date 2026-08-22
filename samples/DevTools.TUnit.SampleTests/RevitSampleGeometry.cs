namespace DevTools.TUnit.SampleTests;

/// <summary>Shared Revit geometry builders for data-source and fixture samples.</summary>
internal static class RevitSampleGeometry
{
    internal static BoundingBoxXYZ Box(
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ) =>
        new()
        {
            Min = new XYZ(minX, minY, minZ),
            Max = new XYZ(maxX, maxY, maxZ),
            Transform = Transform.Identity,
        };
}
