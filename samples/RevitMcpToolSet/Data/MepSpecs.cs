using System.ComponentModel;
namespace RevitMcpToolSet.Data;

public class DuctSpec
{
    [Description("Duct type element ID")] public long DuctTypeId { get; set; }
    [Description("System type element ID")] public long SystemTypeId { get; set; }
    [Description("Level element ID")] public long LevelId { get; set; }
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; }
    [Description("Duct width (for rectangular)")] public double Width { get; set; }
    [Description("Duct height (for rectangular)")] public double Height { get; set; }
}

public class ConduitSpec
{
    [Description("Conduit type element ID")] public long ConduitTypeId { get; set; }
    [Description("Level element ID")] public long LevelId { get; set; }
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; }
    [Description("Conduit diameter")] public double Diameter { get; set; }
}

public class PipeSpec
{
    [Description("Pipe type element ID")] public long PipeTypeId { get; set; }
    [Description("Piping system type element ID")] public long SystemTypeId { get; set; }
    [Description("Level element ID")] public long LevelId { get; set; }
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; }
    [Description("Pipe diameter")] public double Diameter { get; set; }
}

public class LevelSpec
{
    [Description("Name for the level")] public string LevelName { get; set; } = "";
    [Description("Elevation in project units")] public double Elevation { get; set; }
    [Description("Whether to create a floor plan view")] public bool CreateFloorPlanView { get; set; }
}
