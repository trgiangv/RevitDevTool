using DevTools.Logging.Options;
using DevTools.Views.Interfaces;
using RevitDevTool.Logging.Enums;
using RevitDevTool.Settings.Config;

namespace RevitDevTool.Settings;

public interface IRevitSettingsService : IDevToolsSettingsService
{
    HashSet<RevitEnricher> RevitEnrichers { get; set; }
    VisualizationConfig VisualizationConfig { get; }
}
