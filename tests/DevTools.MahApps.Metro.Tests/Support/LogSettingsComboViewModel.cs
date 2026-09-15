using System.Collections.ObjectModel;

namespace DevTools.MetroFork.Tests.Support;

public sealed class LogSettingsComboViewModel
{
    public ObservableCollection<string> SelectedLogTargets { get; } = [];

    public string[] AvailableLogTargets { get; } = { "Monitor", "File", "Http" };

    public void LoadFromSettings(bool fileEnabled, bool httpEnabled)
    {
        SelectedLogTargets.Clear();
        SelectedLogTargets.Add("Monitor");
        if (fileEnabled)
            SelectedLogTargets.Add("File");
        if (httpEnabled)
            SelectedLogTargets.Add("Http");
    }
}
