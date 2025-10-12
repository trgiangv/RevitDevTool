using System.Diagnostics ;
using RevitDevTool.Services ;
using RevitDevTool.Theme ;
using Wpf.Ui.Appearance ;

namespace RevitDevTool.ViewModel.Settings ;

public partial class GeneralSettingsViewModel : ObservableObject
{
    public static readonly GeneralSettingsViewModel Instance = new();
    
    public static List<ApplicationTheme> Themes
    {
#if REVIT2024_OR_GREATER
        get =>
        [
            ApplicationTheme.Light,
            ApplicationTheme.Dark,
            ApplicationTheme.Auto
        ];
#else
        get =>
        [
            ApplicationTheme.Light,
            ApplicationTheme.Dark
        ];
#endif
    }
    
    [ObservableProperty] private ApplicationTheme _theme;

    partial void OnThemeChanged( ApplicationTheme value )
    {
        SettingsService.Instance.GeneralConfig.Theme = value;
        ThemeWatcher.Instance.ApplyTheme();
    }
    
    private GeneralSettingsViewModel()
    {
        Theme = SettingsService.Instance.GeneralConfig.Theme;
    }
    
    [RelayCommand] private void ResetSettings()
    {
        SettingsService.Instance.ResetVisualizationSettings();
        SettingsService.Instance.ResetGeneralSettings();
        Theme = SettingsService.Instance.GeneralConfig.Theme;
        VisualizationController.Refresh();
        Trace.TraceInformation("Reset settings to default");
    }
}