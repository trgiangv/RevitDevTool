using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Options;

[Serializable]
public class TraceListenerOptions
{
    [JsonPropertyName("logLevel")]
    public LogLevel LogLevel { get; set; } = LogLevel.Debug;

    [JsonPropertyName("includeStackTrace")]
    public bool IncludeStackTrace { get; set; }

    [JsonPropertyName("stackTraceDepth")]
    public int StackTraceDepth { get; set; } = 3;

    [JsonPropertyName("includeWpfTrace")]
    public bool IncludeWpfTrace { get; set; }

    [JsonPropertyName("wpfTraceLevel")]
    public SourceLevels WpfTraceLevel { get; set; } = SourceLevels.Warning;

    [JsonPropertyName("filterKeywords")]
    public LogLevelKeys LevelKeys { get; set; } = new();
}
