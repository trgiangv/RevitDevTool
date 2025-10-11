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
    
    [RelayCommand] private static void ResetVisualizationSettings()
    {
        SettingsService.Instance.ResetVisualizationSettings();
    }
    
    public GeneralSettingsViewModel()
    {
        Theme = SettingsService.Instance.GeneralConfig.Theme;
    }
}