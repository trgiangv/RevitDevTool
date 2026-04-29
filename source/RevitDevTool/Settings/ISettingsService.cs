using DevTools.Settings;
using RevitDevTool.Logging.Enums;
using RevitDevTool.Settings.Config;

namespace RevitDevTool.Settings;

public interface IRevitSettingsService : ISettingsService
{
    HashSet<RevitEnricher> RevitEnrichers { get; set; }
    VisualizationConfig VisualizationConfig { get; }
}
