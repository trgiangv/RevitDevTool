using System.Text.Json.Serialization;

namespace DevTools.Settings.Configs;

[Serializable]
public sealed class GeneralConfig
{
    [JsonPropertyName("theme")]
    public AppTheme Theme { get; set; } = AppTheme.Light;

    [JsonPropertyName("isTraceEnabled")]
    public bool IsTraceEnabled { get; set; } = true;
    
    [JsonPropertyName("isMemoryEnabled")]
    public bool IsMemoryEnabled { get; set; } = true;

    [JsonPropertyName("enableTelemetry")]
    public bool EnableTelemetry { get; set; } = true;
}
