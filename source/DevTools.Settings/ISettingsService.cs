using DevTools.Settings.Configs;

namespace DevTools.Settings;

/// <summary>
/// Shared settings contract for all host apps.
/// Host-specific settings (e.g. Revit VisualizationConfig) extend this in each host project.
/// </summary>
public interface ISettingsService
{
    GeneralConfig GeneralConfig { get; }
    ExecutionConfig ExecutionConfig { get; }
    McpRegistryConfig McpRegistryConfig { get; }
    LogConfig LogConfig { get; }

    void SaveSettings();
    void LoadSettings();
    void ResetSettings();
}
