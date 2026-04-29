using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using DevTools.Presentation.Interfaces;
using DevTools.Presentation.ViewModels.Messages;
namespace DevTools.Presentation.ViewModels.Settings;

public partial class SettingsViewModel(
    IDevToolsSettingsService settingsService,
    IMessenger messenger,
    IVisualizationBridge? visualization = null) : ObservableObject
{
    [RelayCommand]
    private void ResetSettings()
    {
        settingsService.ResetSettings();
        visualization?.Refresh();
        messenger.Send(new ResetSettingsMessage());
        Trace.TraceInformation("Reset all settings to default");
    }
}
