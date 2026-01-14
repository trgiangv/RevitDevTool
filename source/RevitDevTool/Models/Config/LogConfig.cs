using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace RevitDevTool.Models.Config;

/// <summary>
///     Log save settings
/// </summary>
[Serializable]
public sealed class LogConfig
{
    [JsonPropertyName("LogLevel")]
    public LogLevel LogLevel { get; set; } = LogLevel.Debug;

    [JsonPropertyName("IsSaveLogEnabled")]
    public bool IsSaveLogEnabled { get; set; }

    [JsonPropertyName("UseExternalFileOnly")]
    public bool UseExternalFileOnly { get; set; }

    [JsonPropertyName("SaveFormat")]
    public LogSaveFormat SaveFormat { get; set; } = LogSaveFormat.Text;

    [JsonPropertyName("IncludeStackTrace")]
    public bool IncludeStackTrace { get; set; }

    [JsonPropertyName("IncludeWpfTrace")]
    public bool IncludeWpfTrace { get; set; }

    [JsonPropertyName("WpfTraceLevel")]
    public SourceLevels WpfTraceLevel { get; set; } = SourceLevels.Warning;

    [JsonPropertyName("StackTraceDepth")]
    public int StackTraceDepth { get; set; } = 3;

    [JsonPropertyName("TimeInterval")]
    public RollingInterval TimeInterval { get; set; } = RollingInterval.Day;

    [JsonPropertyName("LogFolder")]
    public string LogFolder { get; set; } = string.Empty;
}

public enum LogSaveFormat
{
    Json,
    Text
}

public enum RollingInterval
{
    Infinite,
    Year,
    Month,
    Day,
    Hour,
    Minute
}