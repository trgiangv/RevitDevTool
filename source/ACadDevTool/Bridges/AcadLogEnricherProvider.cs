using AcadDevTool.Logging.Enums;
using AcadDevTool.Settings;
using DevTools.Presentation.Interfaces;

namespace AcadDevTool.Bridges;

public sealed class AcadLogEnricherProvider(IAcadSettingsService settingsService) : ILogEnricherProvider
{
    public IReadOnlyList<object> AvailableEnrichers { get; } =
        Enum.GetValues(typeof(AcadEnricher)).Cast<object>().ToList();

    public IList<object> SelectedEnrichers
    {
        get => settingsService.AcadEnrichers.Cast<object>().ToList();
        set => settingsService.AcadEnrichers = new HashSet<AcadEnricher>(value.Cast<AcadEnricher>());
    }
}
