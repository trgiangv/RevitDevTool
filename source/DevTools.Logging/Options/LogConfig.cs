using System.Text.Json.Serialization;

namespace DevTools.Logging.Options;

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
