using AcadDevTool.Logging.Enums;
using DevTools.Settings;

namespace AcadDevTool.Settings;

public interface IAcadSettingsService : ISettingsService
{
    HashSet<AcadEnricher> AcadEnrichers { get; set; }
}

