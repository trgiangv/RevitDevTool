using DevTools.Logging.Options;
using RevitDevTool.Logging.Enums;
using RevitDevTool.Settings.Config;

namespace RevitDevTool.Settings;

public interface ISettingsService
{
    GeneralConfig GeneralConfig { get; }
    LogConfig LogConfig { get; }
    HashSet<RevitEnricher> RevitEnrichers { get; set; }
    VisualizationConfig VisualizationConfig { get; }
    CodeExecuteConfig CodeExecuteConfig { get; }
    McpRegistryConfig McpRegistryConfig { get; }

    void SaveSettings();
    void LoadSettings();
    void ResetSettings();
}
