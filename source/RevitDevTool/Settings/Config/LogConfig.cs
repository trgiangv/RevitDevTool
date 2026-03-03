using System.Diagnostics;
using System.Text.Json.Serialization;
using RevitDevTool.Logging.Enums;
using Serilog;
using Serilog.Events;
namespace RevitDevTool.Settings.Config;

/// <summary>
/// Core logging configuration shared across host and console.
/// </summary>
[Serializable]
public class LogConfig
{
    [JsonPropertyName("logLevel")]
    public LogEventLevel LogLevel { get; set; } = LogEventLevel.Debug;

    [JsonPropertyName("isSaveLogEnabled")]
    public bool IsSaveLogEnabled { get; set; }

    [JsonPropertyName("saveFormat")]
    public SaveFormat SaveFormat { get; set; } = SaveFormat.Text;

    [JsonPropertyName("includeStackTrace")]
    public bool IncludeStackTrace { get; set; }

    [JsonPropertyName("stackTraceDepth")]
    public int StackTraceDepth { get; set; } = 3;

    [JsonPropertyName("timeInterval")]
    public RollingInterval TimeInterval { get; set; } = RollingInterval.Day;

    [JsonPropertyName("logFolder")]
    public string LogFolder { get; set; } = string.Empty;

    [JsonPropertyName("filterKeywords")]
    public LogFilterKeywords FilterKeywords { get; set; } = new();

    [JsonPropertyName("autoClean")]
    public bool AutoClean { get; set; } = true;

    [JsonPropertyName("enablePrettyJson")]
    public bool EnablePrettyJson { get; set; }
    
    [JsonPropertyName("useExternalFileOnly")]
    public bool UseExternalFileOnly { get; set; }

    [JsonPropertyName("includeWpfTrace")]
    public bool IncludeWpfTrace { get; set; }

    [JsonPropertyName("wpfTraceLevel")]
    public SourceLevels WpfTraceLevel { get; set; } = SourceLevels.Warning;

    [JsonPropertyName("revitEnrichers")]
    public RevitEnricher RevitEnrichers { get; set; } = RevitEnricher.RevitVersion | RevitEnricher.RevitDocumentTitle;
}

[Serializable]
public class LogFilterKeywords
{
    [JsonPropertyName("information")]
    public string Information { get; set; } = "info,success,completed";

    [JsonPropertyName("warning")]
    public string Warning { get; set; } = "warning,warn,caution";

    [JsonPropertyName("error")]
    public string Error { get; set; } = "error,failed,exception";

    [JsonPropertyName("critical")]
    public string Critical { get; set; } = "fatal,critical,crash";
}
