using AcadDevTool.Logging.Enums;
using DevTools.Views.Interfaces;

namespace AcadDevTool.Settings;

public interface IAcadSettingsService : IDevToolsSettingsService
{
    HashSet<AcadEnricher> AcadEnrichers { get; set; }
}

