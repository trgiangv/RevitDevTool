using DevTools.Presentation.Interfaces;
using RevitDevTool.Logging.Enums;
using RevitDevTool.Settings;

namespace RevitDevTool.Bridges;

public sealed class RevitLogEnricherProvider(IRevitSettingsService settingsService) : ILogEnricherProvider
{
    public IReadOnlyList<object> AvailableEnrichers { get; } =
        Enum.GetValues(typeof(RevitEnricher)).Cast<object>().ToList();

    public IList<object> SelectedEnrichers
    {
        get => settingsService.RevitEnrichers.Cast<object>().ToList();
        set => settingsService.RevitEnrichers = new HashSet<RevitEnricher>(value.Cast<RevitEnricher>());
    }
}
