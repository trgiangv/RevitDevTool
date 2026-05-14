using System.Text.Json.Serialization;
using DevTools.UI.Theme;
namespace DevTools.Settings.Configs;

[Serializable]
public sealed class GeneralConfig
{
    [JsonPropertyName("theme")]
    public AppTheme Theme { get; set; } = AppTheme.Light;
    
    [JsonPropertyName("useHardwareRendering")]
    public bool UseHardwareRendering { get; set; } = true;
    
    [JsonPropertyName("isTraceEnabled")]
    public bool IsTraceEnabled { get; set; } = true;
    
    [JsonPropertyName("isMemoryEnabled")]
    public bool IsMemoryEnabled { get; set; } = true;

    [JsonPropertyName("enableTelemetry")]
    public bool EnableTelemetry { get; set; } = true;
}
