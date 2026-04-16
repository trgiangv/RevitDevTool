using System.ComponentModel;
namespace RevitMcpToolSet.Data;

public class Bounds3D
{
    [Description("Minimum X coordinate")] public double MinX { get; set; }
    [Description("Minimum Y coordinate")] public double MinY { get; set; }
    [Description("Minimum Z coordinate")] public double MinZ { get; set; }
    [Description("Maximum X coordinate")] public double MaxX { get; set; }
    [Description("Maximum Y coordinate")] public double MaxY { get; set; }
    [Description("Maximum Z coordinate")] public double MaxZ { get; set; }
}

public class ElementSummary
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
}

public class ElementDetail : ElementSummary
{
    public string ElementClass { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public string TypeName { get; set; } = "";
    public long TypeId { get; set; }
    public string LevelName { get; set; } = "";
    public Bounds3D? BoundingBox { get; set; }
    public Dictionary<string, string>? KeyParameters { get; set; }
    public string? ProcessingStatus { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ParameterEntry
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string StorageType { get; set; } = "";
    public bool IsReadOnly { get; set; }
    public bool IsShared { get; set; }
    public bool HasValue { get; set; }
    public string? BuiltInParam { get; set; }
}
