using Aprillz.MewUI;
using DevTools.Settings.Configs;
using Microsoft.Win32;

namespace DevTools.Daemon.Desktop;

public sealed class Preferences
{
    private readonly UserSettingsStore _settings;
    private bool _suppressAutoStartSync;

    public ObservableValue<bool> AutoStartEnabled { get; } = new();
    public ObservableValue<AppTheme> Theme { get; }
    public IReadOnlyList<AppTheme> Themes { get; } = Enum.GetValues<AppTheme>();

    public Preferences(UserSettingsStore settings)
    {
        _settings = settings;
        Theme = new ObservableValue<AppTheme>(settings.Current.Theme);

        Theme.Changed += OnThemeChanged;
        AutoStartEnabled.Changed += OnAutoStartChanged;

        ReloadAutoStart();
        ThemeHelper.Apply(Theme.Value);
        SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;
    }

    public void ReloadAutoStart()
    {
        _suppressAutoStartSync = true;
        AutoStartEnabled.Value = AutoStart.IsEnabled;
        _suppressAutoStartSync = false;
    }

    private void OnAutoStartChanged()
    {
        if (_suppressAutoStartSync)
            return;

        var enabled = AutoStartEnabled.Value;
        if (enabled)
            AutoStart.Enable();
        else
            AutoStart.Disable();

        _settings.Update(s => s.AutoStartEnabled = enabled);
    }

    private void OnThemeChanged()
    {
        _settings.Update(s => s.Theme = Theme.Value);
        ThemeHelper.Apply(Theme.Value);
    }

    private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
            return;
        if (Theme.Value != AppTheme.Auto)
            return;

        UiDispatch.Post(() => ThemeHelper.Apply(AppTheme.Auto));
    }
}
