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

    [JsonPropertyName("StackTraceDepth")]
    public int StackTraceDepth { get; set; } = 3;
    
    [JsonPropertyName("TimeInterval")] 
    public RollingInterval TimeInterval { get; set; } = RollingInterval.Infinite;
    
    [JsonPropertyName("FilePath")] 
    public string FilePath { get; set; } = string.Empty;
}

public enum LogSaveFormat
{
    Sqlite,
    Json,
    Clef,
    Text
}