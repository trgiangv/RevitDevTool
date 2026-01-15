using RevitDevTool.Settings.Config;
namespace RevitDevTool.Settings;

/// <summary>
/// Interface for settings service.
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

