using System.Windows;
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
        if (Application.Current is null)
            return;

#pragma warning disable WPF0001 // ThemeMode is experimental
        Application.Current.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System
        };
#pragma warning restore WPF0001

        Changed?.Invoke();
    }
}
