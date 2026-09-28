using System.Windows;
using DevTools.Settings.Configs;
using HandyControl.Data;
using HandyControl.Tools;
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
        var app = Application.Current;
        if (app is null)
            return;

        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(() => Apply(theme));
            return;
        }

        var actual = theme == AppTheme.Auto
            ? IsLight(AppTheme.Auto) ? AppTheme.Light : AppTheme.Dark
            : theme;
        var dictionaries = app.Resources.MergedDictionaries;
        if (dictionaries.Count < 2)
            return;

        dictionaries[0].MergedDictionaries.Clear();
        dictionaries[0].MergedDictionaries.Add(
            ResourceHelper.GetSkin(actual == AppTheme.Dark ? SkinType.Dark : SkinType.Default));
        dictionaries[1].MergedDictionaries.Clear();
        dictionaries[1].MergedDictionaries.Add(ResourceHelper.GetStandaloneTheme());
        foreach (Window window in app.Windows)
            window.OnApplyTemplate();
        Changed?.Invoke();
    }
}
