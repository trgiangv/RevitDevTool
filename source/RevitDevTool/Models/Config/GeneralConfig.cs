using System.Text.Json.Serialization;
using RevitDevTool.Theme;

namespace RevitDevTool.Models.Config;

/// <summary>
///     Settings options saved on disk
/// </summary>
[Serializable]
public sealed class GeneralConfig
{
    [JsonPropertyName("Theme")] public AppTheme Theme { get; set; } = AppTheme.Light;
    [JsonPropertyName("UseHardwareRendering")] public bool UseHardwareRendering { get; set; } = true;
}