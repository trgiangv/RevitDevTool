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
}