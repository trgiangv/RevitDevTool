using System.Text.Json.Serialization;
using ZLogger.Providers;

namespace DevTools.Logging.Options;

[Serializable]
public class FileLoggingOptions
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("format")]
    public SaveFormat Format { get; set; } = SaveFormat.Text;

    [JsonPropertyName("rollingInterval")]
    public RollingInterval RollingInterval { get; set; } = RollingInterval.Day;

    [JsonPropertyName("logFolder")]
    public string LogFolder { get; set; } = string.Empty;

    [JsonPropertyName("autoClean")]
    public bool AutoClean { get; set; } = true;
}
