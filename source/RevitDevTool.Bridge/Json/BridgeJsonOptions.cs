using System.Text.Json;
using System.Text.Json.Serialization;

namespace RevitDevTool.Bridge.Json;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for config file parsing and JSON output.
/// IPC pipe serialization uses MessagePack — see <c>MessagePackSerializer</c>.
/// </summary>
public static class BridgeJsonOptions
{
    public static JsonSerializerOptions Instance { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static JsonSerializerOptions Indented { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
