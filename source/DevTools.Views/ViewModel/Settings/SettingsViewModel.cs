using CommunityToolkit.Mvvm.Messaging;
using DevTools.Views.Interfaces;
using DevTools.Views.ViewModel.Messages;
using System.Diagnostics;

namespace DevTools.Views.ViewModel.Settings;

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
