using CommunityToolkit.Mvvm.Messaging;
using RevitDevTool.Controllers;
using RevitDevTool.Settings;
using RevitDevTool.ViewModel.Messages;
using System.Diagnostics;

namespace RevitDevTool.ViewModel.Settings;

public partial class SettingsViewModel(ISettingsService settingsService, IMessenger messenger) : ObservableObject
{
    [RelayCommand]
    private void ResetSettings()
    {
        settingsService.ResetSettings();
        VisualizationController.Refresh();
        messenger.Send(new ResetSettingsMessage());
        Trace.TraceInformation("Reset all settings to default");
    }
}
