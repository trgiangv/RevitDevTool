using Microsoft.Extensions.Logging;
using RevitDevTool.Logging.Enums;
using System.Diagnostics;
using System.Text.Json.Serialization;
namespace RevitDevTool.Settings.Config;

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

    [JsonPropertyName("EnablePrettyJson")]
    public bool EnablePrettyJson { get; set; } = true;

    [JsonPropertyName("FilterKeywords")]
    public LogFilterKeywords FilterKeywords { get; set; } = new();

    [JsonPropertyName("RevitEnrichers")]
    public RevitEnricher RevitEnrichers { get; set; } = RevitEnricher.RevitVersion | RevitEnricher.RevitBuild;
}

/// <summary>
/// Keywords for detecting LogLevel from message content.
/// Each property contains comma-separated keywords (max 5 per level).
/// </summary>
[Serializable]
public sealed class LogFilterKeywords
{
    /// <summary>
    /// Keywords for Information level (e.g., "info,success,completed")
    /// </summary>
    [JsonPropertyName("Information")]
    public string Information { get; set; } = "info,success,completed";

    /// <summary>
    /// Keywords for Warning level (e.g., "warning,warn,caution")
    /// </summary>
    [JsonPropertyName("Warning")]
    public string Warning { get; set; } = "warning,warn,caution";

    /// <summary>
    /// Keywords for Error level (e.g., "error,failed,exception")
    /// </summary>
    [JsonPropertyName("Error")]
    public string Error { get; set; } = "error,failed,exception";

    /// <summary>
    /// Keywords for Critical level (e.g., "fatal,critical,crash")
    /// </summary>
    [JsonPropertyName("Critical")]
    public string Critical { get; set; } = "fatal,critical,crash";
}