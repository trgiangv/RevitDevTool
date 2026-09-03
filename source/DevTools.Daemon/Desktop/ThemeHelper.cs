using Aprillz.MewUI;
using DevTools.Settings.Configs;
using Microsoft.Win32;
namespace DevTools.Daemon.Desktop;

internal static class ThemeHelper
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    public static event Action? Changed;

    private static bool AppsUseLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(AppsUseLightThemeValue) is int value && value != 0;
    }

    public static bool IsLight(AppTheme theme) => theme switch
    {
        AppTheme.Light => true,
        AppTheme.Dark => false,
        _ => AppsUseLightTheme()
    };

    public static void Apply(AppTheme theme)
    {
        if (!Application.IsRunning)
            return;

        Application.Current.SetThemeMode(theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.System
        });

        Changed?.Invoke();
    }
}
