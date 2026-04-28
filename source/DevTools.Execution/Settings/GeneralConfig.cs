using System.Text.Json.Serialization;
using DevTools.UI.Theme;
namespace DevTools.Execution.Settings;

[Serializable]
public sealed class GeneralConfig
{
    [JsonPropertyName("Theme")] public AppTheme Theme { get; set; } = AppTheme.Light;
    [JsonPropertyName("UseHardwareRendering")] public bool UseHardwareRendering { get; set; } = true;
    [JsonPropertyName("IsTraceEnabled")] public bool IsTraceEnabled { get; set; } = true;
    [JsonPropertyName("IsMemoryEnabled")] public bool IsMemoryEnabled { get; set; } = true;
}
