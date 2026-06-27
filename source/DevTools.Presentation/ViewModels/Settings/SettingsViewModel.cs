using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using ZLogger;
using DevTools.Presentation.Interfaces;
using DevTools.Presentation.ViewModels.Messages;
using DevTools.Settings;
namespace DevTools.Presentation.ViewModels.Settings;

public partial class SettingsViewModel(
    ISettingsService settingsService,
    IMessenger messenger,
    GeneralSettingsViewModel generalSettings,
    LogSettingsViewModel logSettings,
    McpSettingViewModel mcpSettings,
    ILogger<SettingsViewModel> logger,
    IVisualizationBridge? visualization = null) : ObservableObject
{
    public GeneralSettingsViewModel GeneralSettings => generalSettings;

    public LogSettingsViewModel LogSettings => logSettings;

    public McpSettingViewModel McpSettings => mcpSettings;

    [RelayCommand]
    private void ResetSettings()
    {
        settingsService.ResetSettings();
        visualization?.Refresh();
        messenger.Send(new ResetSettingsMessage());
        logger.ZLogInformation($"Reset all settings to default");
    }
}
