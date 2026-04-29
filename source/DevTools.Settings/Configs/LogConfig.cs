using System.Text.Json.Serialization;
using DevTools.Logging.Options;

namespace DevTools.Settings.Configs;

/// <summary>
/// Persisted log sink configuration (JSON under Settings/). Nested option POCOs live in <c>DevTools.Logging.Options</c>.
/// </summary>
[Serializable]
public class LogConfig
{
    [JsonPropertyName("fileLogging")]
    public FileLoggingOptions FileLogging { get; set; } = new();

    [JsonPropertyName("traceListener")]
    public TraceListenerOptions TraceListener { get; set; } = new();

    [JsonPropertyName("monitor")]
    public MonitorLoggingOptions Monitor { get; set; } = new();

    [JsonPropertyName("httpLogging")]
    public HttpLoggingOptions HttpLogging { get; set; } = new();
}
