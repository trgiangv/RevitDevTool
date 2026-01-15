using CommunityToolkit.Mvvm.Messaging;
using RevitDevTool.Controllers;
using RevitDevTool.Settings;
using RevitDevTool.Theme;
using RevitDevTool.ViewModel.Messages;

namespace RevitDevTool.ViewModel.Settings;

public partial class GeneralSettingsViewModel : ObservableObject, IRecipient<ResetSettingsMessage>
{
    private readonly ISettingsService _settingsService;

    public static List<AppTheme> Themes
    {
        get =>
        [
            AppTheme.Light,
            AppTheme.Dark,
#if REVIT2024_OR_GREATER
            AppTheme.Auto
#endif
        ];
    }

    [ObservableProperty] private AppTheme _theme;
    [ObservableProperty] private bool _useHardwareRendering;

    partial void OnThemeChanged(AppTheme value)
    {
        _settingsService.GeneralConfig.Theme = value;
        ThemeManager.Current.ApplySettingsTheme(value);
    }

    partial void OnUseHardwareRenderingChanged(bool value)
    {
        _settingsService.GeneralConfig.UseHardwareRendering = value;
        HostBackgroundController.ToggleHardwareRendering(_settingsService);
    }

    public GeneralSettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadFromConfig();
        WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive(ResetSettingsMessage message)
    {
        LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        Theme = _settingsService.GeneralConfig.Theme;
        UseHardwareRendering = _settingsService.GeneralConfig.UseHardwareRendering;
    }
}
