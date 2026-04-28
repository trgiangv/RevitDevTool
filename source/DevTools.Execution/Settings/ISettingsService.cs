using DevTools.Execution.Configs;

namespace DevTools.Execution.Settings;

/// <summary>
/// Shared settings contract for all host apps.
/// Host-specific settings (e.g. Revit VisualizationConfig) extend this in each host project.
/// </summary>
public interface ISettingsService
{
    GeneralConfig GeneralConfig { get; }
    ExecutionConfig ExecutionConfig { get; }
    McpRegistryConfig McpRegistryConfig { get; }
    void SaveSettings();
    void LoadSettings();
}
