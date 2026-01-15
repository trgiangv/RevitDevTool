using RevitDevTool.Controllers;
using RevitDevTool.Settings;
using RevitDevTool.Theme;
using System.Diagnostics;

namespace RevitDevTool.ViewModel.Settings;

public partial class GeneralSettingsViewModel : ObservableObject
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
        Theme = _settingsService.GeneralConfig.Theme;
        UseHardwareRendering = _settingsService.GeneralConfig.UseHardwareRendering;
    }

    [RelayCommand]
    private void ResetSettings()
    {
        _settingsService.ResetSettings();
        Theme = _settingsService.GeneralConfig.Theme;
        UseHardwareRendering = _settingsService.GeneralConfig.UseHardwareRendering;
        VisualizationController.Refresh();
        Trace.TraceInformation("Reset settings to default");
    }
}
