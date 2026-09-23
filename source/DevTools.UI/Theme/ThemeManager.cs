using System.Windows;

namespace DevTools.UI.Theme;

/// <summary>
/// Manages application theme with host-app integration via delegate callbacks.
/// Call <see cref="Setup"/> once at startup to wire the host theme provider.
/// </summary>
public sealed class ThemeManager : DependencyObject
{
    private bool _useHostTheme;
    private Func<AppTheme>? _resolveHostTheme;

    private ThemeManager() { }

    /// <summary>
    /// Initializes the theme manager with host-specific callbacks.
    /// </summary>
    /// <param name="resolveHostTheme">Returns the current host app theme (called when Auto).</param>
    /// <param name="subscribeToChanges">
    /// Called with an <see cref="Action"/> that the host should invoke when its theme changes at runtime.
    /// </param>
    public static void Setup(Func<AppTheme> resolveHostTheme, Action<Action>? subscribeToChanges = null)
    {
        Current._resolveHostTheme = resolveHostTheme;
        subscribeToChanges?.Invoke(Current.OnHostThemeChanged);
    }

    private AppTheme ResolveAutoTheme() => _resolveHostTheme?.Invoke() ?? AppTheme.Light;

    private void OnHostThemeChanged()
    {
        if (!_useHostTheme) return;
        ActualApplicationTheme = ResolveAutoTheme();
        ApplyThemeToResources();
    }

    public static readonly DependencyProperty ApplicationThemeProperty =
        DependencyProperty.Register(
            nameof(ApplicationTheme),
            typeof(AppTheme?),
            typeof(ThemeManager),
            new PropertyMetadata(OnApplicationThemeChanged));

    public AppTheme? ApplicationTheme
    {
        get => (AppTheme?)GetValue(ApplicationThemeProperty);
        set => SetValue(ApplicationThemeProperty, value);
    }

    private static void OnApplicationThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ThemeManager)d).UpdateActualApplicationTheme();
    }

    private static readonly DependencyPropertyKey ActualApplicationThemePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ActualApplicationTheme),
            typeof(AppTheme),
            typeof(ThemeManager),
            new PropertyMetadata(AppTheme.Light, OnActualApplicationThemeChanged));

    public static readonly DependencyProperty ActualApplicationThemeProperty =
        ActualApplicationThemePropertyKey.DependencyProperty;

    public AppTheme ActualApplicationTheme
    {
        get => (AppTheme)GetValue(ActualApplicationThemeProperty);
        private set => SetValue(ActualApplicationThemePropertyKey, value);
    }

    private static void OnActualApplicationThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var tm = (ThemeManager)d;
        // tm.ApplyThemeToResources();
        tm.ActualApplicationThemeChanged?.Invoke(tm, EventArgs.Empty);
    }

    private void UpdateActualApplicationTheme()
    {
        var theme = ApplicationTheme ?? AppTheme.Auto;
        if (theme == AppTheme.Auto)
        {
            _useHostTheme = true;
            ActualApplicationTheme = ResolveAutoTheme();
        }
        else
        {
            _useHostTheme = false;
            ActualApplicationTheme = theme;
        }
    }

    private void ApplyThemeToResources()
    {
        ThemeResources.Current?.ApplyApplicationTheme(ActualApplicationTheme);
        FluentThemeResources.Current?.ApplyApplicationTheme(ActualApplicationTheme);
    }

    public static ThemeManager Current { get; } = new();
    public event EventHandler<EventArgs>? ActualApplicationThemeChanged;

    /// <summary>
    /// Called after settings are loaded to apply the saved theme.
    /// </summary>
    public void ApplySettingsTheme(AppTheme theme)
    {
        ApplicationTheme = theme;
        UpdateActualApplicationTheme();
        ApplyThemeToResources();
    }
}
