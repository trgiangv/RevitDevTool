using System.Diagnostics;
using System.Text.Json.Serialization;
using Serilog;
namespace RevitDevTool.Models.Config;

/// <summary>
///     Log save settings
/// </summary>
[Serializable]
public sealed class LogConfig
{
    [JsonPropertyName("IsSaveLogEnabled")] 
    public bool IsSaveLogEnabled { get; set; }
    
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
    
    [JsonPropertyName("FilePath")] 
    public string FilePath { get; set; } = SettingsLocation.GetDefaultLogPath("log");
}

public enum LogSaveFormat
{
    Sqlite,
    Json,
    Clef,
    Text
}