using System.Text.Json.Serialization ;
using Wpf.Ui.Appearance ;

namespace RevitDevTool.Models.Config;

/// <summary>
///     Settings options saved on disk
/// </summary>
[Serializable]
public sealed class GeneralConfig
{
    [JsonPropertyName("Theme")] public ApplicationTheme Theme { get; set; } = ApplicationTheme.Light;
    [JsonPropertyName("UseHardwareRendering")] public bool UseHardwareRendering { get; set; } = true;
    [JsonPropertyName("LogSettings")] public LogConfig LogConfig { get; set; } = new();
}

/// <summary>
///     Log save settings
/// </summary>
[Serializable]
public sealed class LogConfig
{
    [JsonPropertyName("SaveFormat")] 
    public LogSaveFormat SaveFormat { get; set; } = LogSaveFormat.Json;
    
    [JsonPropertyName("IncludeStackTrace")] 
    public bool IncludeStackTrace { get; set; }
    
    [JsonPropertyName("UseMultipleFiles")] 
    public bool UseMultipleFiles { get; set; }
    
    [JsonPropertyName("TimeInterval")] 
    public LogTimeInterval TimeInterval { get; set; } = LogTimeInterval.Day;
    
    [JsonPropertyName("FilePath")] 
    public string FilePath { get; set; } = string.Empty;
    
    [JsonPropertyName("FolderPath")] 
    public string FolderPath { get; set; } = string.Empty;
}

public enum LogSaveFormat
{
    Sqlite,
    Json,
    Xml,
    Csv,
    Log
}

public enum LogTimeInterval
{
    Minute,
    Hour,
    Day,
    Week
}