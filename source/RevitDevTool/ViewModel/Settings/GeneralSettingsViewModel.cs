using System.Diagnostics ;
using RevitDevTool.Services ;
using RevitDevTool.Theme ;
using RevitDevTool.View ;
using RevitDevTool.View.Settings ;
using Wpf.Ui.Appearance ;
using Wpf.Ui.Controls ;

namespace RevitDevTool.ViewModel.Settings ;

public partial class GeneralSettingsViewModel : ObservableObject
{
    public static readonly GeneralSettingsViewModel Instance = new();
    
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
        SettingsService.Instance.GeneralConfig.Theme = value;
        ThemeWatcher.Instance.ApplyTheme();
    }
    
    partial void OnUseHardwareRenderingChanged(bool value)
    {
        SettingsService.Instance.GeneralConfig.UseHardwareRendering = value;
        if (value) Application.EnableHardwareRendering();
        else Application.DisableHardwareRendering();
    }
    
    private GeneralSettingsViewModel()
    {
        Theme = SettingsService.Instance.GeneralConfig.Theme;
        UseHardwareRendering = SettingsService.Instance.GeneralConfig.UseHardwareRendering;
    }
    
    [RelayCommand] private void ResetSettings()
    {
        SettingsService.Instance.ResetVisualizationSettings();
        SettingsService.Instance.ResetGeneralSettings();
        Theme = SettingsService.Instance.GeneralConfig.Theme;
        UseHardwareRendering = SettingsService.Instance.GeneralConfig.UseHardwareRendering;
        VisualizationController.Refresh();
        Trace.TraceInformation("Reset settings to default");
    }
    
    [RelayCommand] 
    private static async Task SaveLogSettingsAsync()
    {
        var settingsWindow = SettingsWindow.Instance;

        var dialogHost = settingsWindow?.ContentDialogService.GetDialogHost();
        if (dialogHost == null) return;

        var dialog = new LogSettingsView(dialogHost);

        var result = await dialog.ShowAsync(CancellationToken.None).ConfigureAwait(true);

        if (result == ContentDialogResult.Primary)
        {
            Trace.TraceInformation("Log settings saved");
        }
    }
}