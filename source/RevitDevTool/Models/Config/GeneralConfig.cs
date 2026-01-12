using RevitDevTool.Theme;
using System.Text.Json.Serialization;

namespace RevitDevTool.Models.Config;

/// <summary>
///     Settings options saved on disk
/// </summary>
[Serializable]
public sealed class GeneralConfig
{
#if REVIT2024_OR_GREATER
    [JsonPropertyName("Theme")] public AppTheme Theme { get; set; } = AppTheme.Auto;
#else
    [JsonPropertyName("Theme")] public AppTheme Theme { get; set; } = AppTheme.Light;
#endif
    [JsonPropertyName("UseHardwareRendering")] public bool UseHardwareRendering { get; set; } = true;
}