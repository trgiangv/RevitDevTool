using Microsoft.Extensions.Logging;
using RevitDevTool.Logger.Enums;
using System.Text.Json.Serialization;

namespace RevitDevTool.Logger.Config;

/// <summary>
/// Core logging configuration shared across host and console.
/// </summary>
[Serializable]
public class LogConfigCore
{
    [JsonPropertyName("logLevel")]
    public LogLevel LogLevel { get; set; } = LogLevel.Debug;

    [JsonPropertyName("isSaveLogEnabled")]
    public bool IsSaveLogEnabled { get; set; }

    [JsonPropertyName("saveFormat")]
    public LogSaveFormat SaveFormat { get; set; } = LogSaveFormat.Text;

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

    [JsonPropertyName("enablePipeLogBridge")]
    public bool EnablePipeLogBridge { get; set; } = true;

    [JsonPropertyName("enablePrettyJson")]
    public bool EnablePrettyJson { get; set; }
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
