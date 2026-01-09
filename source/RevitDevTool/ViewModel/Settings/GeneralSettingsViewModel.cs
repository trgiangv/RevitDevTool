using System.Diagnostics ;
using RevitDevTool.Services ;
using RevitDevTool.Theme ;
using Wpf.Ui.Appearance ;

namespace RevitDevTool.ViewModel.Settings ;

public partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeWatcherService _themeWatcherService;
    
    public static List<ApplicationTheme> Themes
    {
        get =>
        [
            ApplicationTheme.Light,
            ApplicationTheme.Dark,
#if REVIT2024_OR_GREATER
            ApplicationTheme.Auto
#endif
        ];
    }
    
    [ObservableProperty] private ApplicationTheme _theme;
    [ObservableProperty] private bool _useHardwareRendering;

    partial void OnThemeChanged( ApplicationTheme value )
    {
        _settingsService.GeneralConfig.Theme = value;
        _themeWatcherService.ApplyTheme();
    }
    
    partial void OnUseHardwareRenderingChanged(bool value)
    {
        _settingsService.GeneralConfig.UseHardwareRendering = value;
        if (value) Application.EnableHardwareRendering();
        else Application.DisableHardwareRendering();
    }
    
    public GeneralSettingsViewModel(ISettingsService settingsService, IThemeWatcherService themeWatcherService)
    {
        _settingsService = settingsService;
        _themeWatcherService = themeWatcherService;
        Theme = _settingsService.GeneralConfig.Theme;
        UseHardwareRendering = _settingsService.GeneralConfig.UseHardwareRendering;
    }
    
    [RelayCommand] private void ResetSettings()
    {
        _settingsService.ResetSettings();
        Theme = _settingsService.GeneralConfig.Theme;
        UseHardwareRendering = _settingsService.GeneralConfig.UseHardwareRendering;
        VisualizationController.Refresh();
        Trace.TraceInformation("Reset settings to default");
    }
}