using System.ComponentModel;
using System.Text.Json.Serialization;

namespace RevitMcpToolSet.Data;

/// <summary>
/// Single family instance placement for <c>revit_place_family</c>.
/// </summary>
public class Placement
{
    [Description("X coordinate in feet.")]
    [JsonPropertyName("x")]
    public double X { get; set; }

    [Description("Y coordinate in feet.")]
    [JsonPropertyName("y")]
    public double Y { get; set; }

    [Description("Z coordinate in feet.")]
    [JsonPropertyName("z")]
    public double Z { get; set; }

    [Description("Rotation in degrees around Z axis. Default 0.")]
    [JsonPropertyName("rotation")]
    public double? Rotation { get; set; }

    [Description("Level name for placement. Required for level-based families.")]
    [JsonPropertyName("levelName")]
    public string? LevelName { get; set; }

    [Description("Host element ID for face- or wall-hosted families.")]
    [JsonPropertyName("hostId")]
    public long? HostId { get; set; }
}
