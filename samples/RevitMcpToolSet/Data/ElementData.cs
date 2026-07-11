using System.ComponentModel;
using System.Text.Json.Serialization;

namespace RevitMcpToolSet.Data;

/// <summary>
/// Axis-aligned bounding box. All coordinates are in feet (Revit internal units).
/// </summary>
public class Bounds3D
{
    [Description("Minimum X coordinate in feet.")]
    [JsonIgnore]
    public double MinX { get; set; }

    [Description("Minimum Y coordinate in feet.")]
    [JsonIgnore]
    public double MinY { get; set; }

    [Description("Minimum Z coordinate in feet.")]
    [JsonIgnore]
    public double MinZ { get; set; }

    [Description("Maximum X coordinate in feet.")]
    [JsonIgnore]
    public double MaxX { get; set; }

    [Description("Maximum Y coordinate in feet.")]
    [JsonIgnore]
    public double MaxY { get; set; }

    [Description("Maximum Z coordinate in feet.")]
    [JsonIgnore]
    public double MaxZ { get; set; }

    [Description("[x, y, z] minimum corner in feet.")]
    [JsonPropertyName("min")]
    public double[] Min
    {
        get => [MinX, MinY, MinZ];
        set
        {
            if (value is { Length: >= 3 })
            {
                MinX = value[0];
                MinY = value[1];
                MinZ = value[2];
            }
        }
    }

    [Description("[x, y, z] maximum corner in feet.")]
    [JsonPropertyName("max")]
    public double[] Max
    {
        get => [MaxX, MaxY, MaxZ];
        set
        {
            if (value is { Length: >= 3 })
            {
                MaxX = value[0];
                MaxY = value[1];
                MaxZ = value[2];
            }
        }
    }
}

public class ParameterEntry
{
    [Description("Parameter name.")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [Description("Parameter value as string.")]
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [Description("Storage type: String, Integer, Double, ElementId, etc.")]
    [JsonPropertyName("storage")]
    public string Storage { get; set; } = "";

    [Description("Whether the parameter can be written.")]
    [JsonPropertyName("writable")]
    public bool Writable { get; set; }

    [Description("Whether the parameter is a built-in parameter.")]
    [JsonPropertyName("builtin")]
    public bool Builtin { get; set; }

    [Description("Whether the parameter is shared.")]
    [JsonPropertyName("isShared")]
    public bool IsShared { get; set; }

    [JsonIgnore]
    public string StorageType
    {
        get => Storage;
        set => Storage = value;
    }

    [JsonIgnore]
    public bool IsReadOnly
    {
        get => !Writable;
        set => Writable = !value;
    }

    [Description("Whether the parameter currently has a value.")]
    [JsonIgnore]
    public bool HasValue { get; set; }

    [JsonIgnore]
    public string? BuiltInParam { get; set; }
}
