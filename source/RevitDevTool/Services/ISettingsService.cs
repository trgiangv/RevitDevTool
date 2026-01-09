using RevitDevTool.Models.Config;

namespace RevitDevTool.Services;

/// <summary>
/// Interface for settings service
/// </summary>
public interface ISettingsService
{
    GeneralConfig GeneralConfig { get; }
    LogConfig LogConfig { get; }
    VisualizationConfig VisualizationConfig { get; }
    
    void SaveSettings();
    void LoadSettings();
    void ResetSettings();
}

