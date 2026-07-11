using System.ComponentModel;
using System.Text.Json.Serialization;

namespace RevitMcpToolSet.Data;

/// <summary>
/// Duct segment placement specification. All geometry values are in feet (Revit internal units).
/// </summary>
public class DuctSpec
{
    [Description("Duct type element ID.")]
    [JsonPropertyName("ductTypeId")]
    public long DuctTypeId { get; set; }

    [Description("MEP system type element ID.")]
    [JsonPropertyName("systemTypeId")]
    public long SystemTypeId { get; set; }

    [Description("Level element ID.")]
    [JsonPropertyName("levelId")]
    public long LevelId { get; set; }

    [Description("Start point [x, y, z] in feet.")]
    [JsonPropertyName("start")]
    public double[] Start { get; set; } = [0, 0, 0];

    [Description("End point [x, y, z] in feet.")]
    [JsonPropertyName("end")]
    public double[] End { get; set; } = [0, 0, 0];

    [Description("Duct width in feet (rectangular ducts). Omit or set 0 when not applicable.")]
    [JsonPropertyName("width")]
    public double Width { get; set; }

    [Description("Duct height in feet (rectangular ducts). Omit or set 0 when not applicable.")]
    [JsonPropertyName("height")]
    public double Height { get; set; }

    [Description("Duct diameter in feet (round ducts). Omit or set 0 when not applicable.")]
    [JsonPropertyName("diameter")]
    public double Diameter { get; set; }

    [Description("Slope ratio, e.g. 0.01 = 1%.")]
    [JsonPropertyName("slope")]
    public double? Slope { get; set; }

    [JsonIgnore]
    public double StartX
    {
        get => GetCoord(Start, 0);
        set => Start = SetCoord(Start, 0, value);
    }

    [JsonIgnore]
    public double StartY
    {
        get => GetCoord(Start, 1);
        set => Start = SetCoord(Start, 1, value);
    }

    [JsonIgnore]
    public double StartZ
    {
        get => GetCoord(Start, 2);
        set => Start = SetCoord(Start, 2, value);
    }

    [JsonIgnore]
    public double EndX
    {
        get => GetCoord(End, 0);
        set => End = SetCoord(End, 0, value);
    }

    [JsonIgnore]
    public double EndY
    {
        get => GetCoord(End, 1);
        set => End = SetCoord(End, 1, value);
    }

    [JsonIgnore]
    public double EndZ
    {
        get => GetCoord(End, 2);
        set => End = SetCoord(End, 2, value);
    }

    private static double GetCoord(double[] point, int index)
        => point.Length > index ? point[index] : 0;

    private static double[] SetCoord(double[] point, int index, double value)
    {
        var coords = point.Length > index ? point : Pad(point, index + 1);
        coords[index] = value;
        return coords;
    }

    private static double[] Pad(double[] point, int length)
    {
        var coords = new double[length];
        Array.Copy(point, coords, point.Length);
        return coords;
    }
}

/// <summary>
/// Conduit segment placement specification. All geometry values are in feet.
/// </summary>
public class ConduitSpec
{
    [Description("Conduit type element ID.")]
    [JsonPropertyName("conduitTypeId")]
    public long ConduitTypeId { get; set; }

    [Description("Electrical system type element ID.")]
    [JsonPropertyName("systemTypeId")]
    public long SystemTypeId { get; set; }

    [Description("Level element ID.")]
    [JsonPropertyName("levelId")]
    public long LevelId { get; set; }

    [Description("Start point [x, y, z] in feet.")]
    [JsonPropertyName("start")]
    public double[] Start { get; set; } = [0, 0, 0];

    [Description("End point [x, y, z] in feet.")]
    [JsonPropertyName("end")]
    public double[] End { get; set; } = [0, 0, 0];

    [Description("Conduit diameter in feet.")]
    [JsonPropertyName("diameter")]
    public double Diameter { get; set; }

    [JsonIgnore]
    public double StartX
    {
        get => GetCoord(Start, 0);
        set => Start = SetCoord(Start, 0, value);
    }

    [JsonIgnore]
    public double StartY
    {
        get => GetCoord(Start, 1);
        set => Start = SetCoord(Start, 1, value);
    }

    [JsonIgnore]
    public double StartZ
    {
        get => GetCoord(Start, 2);
        set => Start = SetCoord(Start, 2, value);
    }

    [JsonIgnore]
    public double EndX
    {
        get => GetCoord(End, 0);
        set => End = SetCoord(End, 0, value);
    }

    [JsonIgnore]
    public double EndY
    {
        get => GetCoord(End, 1);
        set => End = SetCoord(End, 1, value);
    }

    [JsonIgnore]
    public double EndZ
    {
        get => GetCoord(End, 2);
        set => End = SetCoord(End, 2, value);
    }

    private static double GetCoord(double[] point, int index)
        => point.Length > index ? point[index] : 0;

    private static double[] SetCoord(double[] point, int index, double value)
    {
        var coords = point.Length > index ? point : Pad(point, index + 1);
        coords[index] = value;
        return coords;
    }

    private static double[] Pad(double[] point, int length)
    {
        var coords = new double[length];
        Array.Copy(point, coords, point.Length);
        return coords;
    }
}

/// <summary>
/// Pipe segment placement specification. All geometry values are in feet.
/// </summary>
public class PipeSpec
{
    [Description("Pipe type element ID.")]
    [JsonPropertyName("pipeTypeId")]
    public long PipeTypeId { get; set; }

    [Description("Piping system type element ID.")]
    [JsonPropertyName("systemTypeId")]
    public long SystemTypeId { get; set; }

    [Description("Level element ID.")]
    [JsonPropertyName("levelId")]
    public long LevelId { get; set; }

    [Description("Start point [x, y, z] in feet.")]
    [JsonPropertyName("start")]
    public double[] Start { get; set; } = [0, 0, 0];

    [Description("End point [x, y, z] in feet.")]
    [JsonPropertyName("end")]
    public double[] End { get; set; } = [0, 0, 0];

    [Description("Pipe diameter in feet.")]
    [JsonPropertyName("diameter")]
    public double Diameter { get; set; }

    [Description("Slope ratio, e.g. 0.01 = 1%.")]
    [JsonPropertyName("slope")]
    public double? Slope { get; set; }

    [JsonIgnore]
    public double StartX
    {
        get => GetCoord(Start, 0);
        set => Start = SetCoord(Start, 0, value);
    }

    [JsonIgnore]
    public double StartY
    {
        get => GetCoord(Start, 1);
        set => Start = SetCoord(Start, 1, value);
    }

    [JsonIgnore]
    public double StartZ
    {
        get => GetCoord(Start, 2);
        set => Start = SetCoord(Start, 2, value);
    }

    [JsonIgnore]
    public double EndX
    {
        get => GetCoord(End, 0);
        set => End = SetCoord(End, 0, value);
    }

    [JsonIgnore]
    public double EndY
    {
        get => GetCoord(End, 1);
        set => End = SetCoord(End, 1, value);
    }

    [JsonIgnore]
    public double EndZ
    {
        get => GetCoord(End, 2);
        set => End = SetCoord(End, 2, value);
    }

    private static double GetCoord(double[] point, int index)
        => point.Length > index ? point[index] : 0;

    private static double[] SetCoord(double[] point, int index, double value)
    {
        var coords = point.Length > index ? point : Pad(point, index + 1);
        coords[index] = value;
        return coords;
    }

    private static double[] Pad(double[] point, int length)
    {
        var coords = new double[length];
        Array.Copy(point, coords, point.Length);
        return coords;
    }
}

public class GridAxisSpec
{
    [Description("Number of grid lines along this axis.")]
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [Description("Spacing between grid lines in feet.")]
    [JsonPropertyName("spacing")]
    public double Spacing { get; set; }
}

public class LevelSpec
{
    [Description("Name for the level.")]
    [JsonPropertyName("name")]
    public string LevelName { get; set; } = "";

    [Description("Elevation in project units (feet).")]
    [JsonPropertyName("elevation")]
    public double Elevation { get; set; }

    [Description("Whether to create a floor plan view.")]
    [JsonPropertyName("createView")]
    public bool CreateFloorPlanView { get; set; }
}
