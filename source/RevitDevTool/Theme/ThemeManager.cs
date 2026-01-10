using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
#if REVIT2024_OR_GREATER
using Autodesk.Revit.UI;
#endif

namespace RevitDevTool.Theme;

/// <summary>
/// Manages application theme with Revit integration support.
/// </summary>
public class ThemeManager : DependencyObject
{
    private bool _isInitialized;
    private bool _applicationInitialized;

    private ThemeManager()
    {
#if REVIT2024_OR_GREATER
        UIFramework.ApplicationTheme.CurrentTheme.PropertyChanged += OnRevitThemeChanged;
#endif
    }

    #region RevitTheme

    private bool _useRevitTheme;

    public bool UseRevitTheme
    {
        get => _useRevitTheme;
        set
        {
            if (_useRevitTheme == value) return;
            _useRevitTheme = value;
            if (value)
                InitRevitTheme();
        }
    }

    private void InitRevitTheme()
    {
#if REVIT2024_OR_GREATER
        var currentTheme = UIThemeManager.CurrentTheme == UITheme.Dark
            ? AppTheme.Dark
            : AppTheme.Light;
        ApplicationTheme = currentTheme;
#endif
    }

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

        Dispatcher.CurrentDispatcher.BeginInvoke(() =>
        {
            if (!UseRevitTheme) return;
            var changedTheme = GetRevitTheme();
            ApplicationTheme = changedTheme;
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
        tm.ApplyApplicationTheme();
        tm.ActualApplicationThemeChanged?.Invoke(tm, EventArgs.Empty);
    }

    private void UpdateActualApplicationTheme()
    {
        ActualApplicationTheme = ApplicationTheme ?? AppTheme.Light;
    }

    private void ApplyApplicationTheme()
    {
        if (!_applicationInitialized) return;
        Debug.Assert(ThemeResources.Current != null);
        ThemeResources.Current.ApplyApplicationTheme(ActualApplicationTheme);
    }

    #endregion

    public static ThemeManager Current { get; } = new();
    public event EventHandler<EventArgs>? ActualApplicationThemeChanged;

    internal void Initialize()
    {
        if (_isInitialized) return;
        UpdateActualApplicationTheme();
        _applicationInitialized = true;
        ApplyApplicationTheme();
        _isInitialized = true;
    }
}
