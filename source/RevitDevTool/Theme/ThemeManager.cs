using System.Windows;
using RevitDevTool.Utils;
#if REVIT2024_OR_GREATER
using Autodesk.Revit.UI;
#endif

namespace RevitDevTool.Theme;

/// <summary>
/// Manages application theme with Revit integration support.
/// </summary>
public class ThemeManager : DependencyObject
{
    private ThemeManager()
    {
#if REVIT2024_OR_GREATER
        UIFramework.ApplicationTheme.CurrentTheme.PropertyChanged += OnRevitThemeChanged;
#endif
    }

    #region RevitTheme

    private bool UseRevitTheme { get; set; }

#if REVIT2024_OR_GREATER
    private static AppTheme GetRevitTheme()
    {
        return UIThemeManager.CurrentTheme == UITheme.Dark
            ? AppTheme.Dark
            : AppTheme.Light;
    }

    private void OnRevitThemeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(UIFramework.ApplicationTheme.CurrentTheme.RibbonPanelBackgroundBrush)) return;
        if (UIThemeManager.CurrentTheme.ToString() == UIFramework.ApplicationTheme.CurrentTheme.RibbonTheme.Name) return;

        DispatcherUtils.RunOnMainThread(() =>
        {
            if (!UseRevitTheme) return;
            ActualApplicationTheme = GetRevitTheme();
            ApplyThemeToResources();
        });
    }
#endif

    #endregion

    #region ApplicationTheme

    /// <summary>
    /// Identifies the ApplicationTheme dependency property.
    /// </summary>
    public static readonly DependencyProperty ApplicationThemeProperty =
        DependencyProperty.Register(
            nameof(ApplicationTheme),
            typeof(AppTheme?),
            typeof(ThemeManager),
            new PropertyMetadata(OnApplicationThemeChanged));

    /// <summary>
    /// Gets or sets a value that determines the light-dark preference for the overall theme of an app.
    /// </summary>
    public AppTheme? ApplicationTheme
    {
        get => (AppTheme?)GetValue(ApplicationThemeProperty);
        set => SetValue(ApplicationThemeProperty, value);
    }

    private static void OnApplicationThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ThemeManager)d).UpdateActualApplicationTheme();
    }

    #endregion

    #region ActualApplicationTheme

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
        tm.ActualApplicationThemeChanged?.Invoke(tm, EventArgs.Empty);
    }

    private void UpdateActualApplicationTheme()
    {
        var theme = ApplicationTheme ?? AppTheme.Auto;
        if (theme == AppTheme.Auto)
        {
#if REVIT2024_OR_GREATER
            UseRevitTheme = true;
            ActualApplicationTheme = GetRevitTheme();
#else
            ActualApplicationTheme = AppTheme.Light;
#endif
        }
        else
        {
            UseRevitTheme = false;
            ActualApplicationTheme = theme;
        }
    }

    private void ApplyThemeToResources()
    {
        if (ThemeResources.Current == null) return;
        ThemeResources.Current.ApplyApplicationTheme(ActualApplicationTheme);
    }

    #endregion

    public static ThemeManager Current { get; } = new();
    public event EventHandler<EventArgs>? ActualApplicationThemeChanged;

    /// <summary>
    /// Called after settings are loaded to apply the saved theme.
    /// Use to apply theme changes at runtime.
    /// </summary>
    public void ApplySettingsTheme(AppTheme theme)
    {
        ApplicationTheme = theme;
        UpdateActualApplicationTheme();
        ApplyThemeToResources();
    }
}
