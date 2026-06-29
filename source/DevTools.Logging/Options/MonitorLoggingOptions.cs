using System.Text.Json.Serialization;

namespace DevTools.Logging.Options;

[Serializable]
public class MonitorLoggingOptions
{
    [JsonPropertyName("enablePrettyJson")]
    public bool EnablePrettyJson { get; set; }
}
