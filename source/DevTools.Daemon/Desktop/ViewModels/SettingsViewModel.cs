using CommunityToolkit.Mvvm.ComponentModel;
using DevTools.Daemon.Control;
using DevTools.Daemon.Desktop.Helpers;
using DevTools.Daemon.Desktop.Models;
using DevTools.Settings.Configs;
using Microsoft.Win32;

namespace DevTools.Daemon.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly UserSettingsStore _settings;
    private bool _suppressAutoStartSync;

    [ObservableProperty]
    public partial bool AutoStartEnabled { get; set; }

    [ObservableProperty]
    public partial AppTheme Theme { get; set; }

    public IReadOnlyList<AppTheme> Themes { get; } = Enum.GetValues<AppTheme>();

    public SettingsViewModel(UserSettingsStore settings)
    {
        _settings = settings;
        Theme = settings.Current.Theme;

        ReloadAutoStart();
        ThemeHelper.Apply(Theme);
        SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;
    }

    public void ReloadAutoStart()
    {
        _suppressAutoStartSync = true;
        AutoStartEnabled = StartupRegistration.IsEnabled;
        _suppressAutoStartSync = false;
    }

    partial void OnAutoStartEnabledChanged(bool value)
    {
        if (_suppressAutoStartSync)
            return;

        if (value)
            StartupRegistration.Enable();
        else
            StartupRegistration.Disable();

        _settings.Update(s => s.AutoStartEnabled = value);
    }

    partial void OnThemeChanged(AppTheme value)
    {
        _settings.Update(s => s.Theme = value);
        ThemeHelper.Apply(value);
    }

    private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
            return;
        if (Theme != AppTheme.Auto)
            return;

        UiDispatchHelper.Post(() => ThemeHelper.Apply(AppTheme.Auto));
    }
}
