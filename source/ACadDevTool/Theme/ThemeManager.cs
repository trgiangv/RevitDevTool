using System.Windows;
using AcadDevTool.Utils;
namespace AcadDevTool.Theme;

/// <summary>
/// Manages application theme with AutoCAD integration support.
/// AutoCAD exposes COLORTHEME system variable (0 = Dark, 1 = Light) on all versions.
/// </summary>
public class ThemeManager : DependencyObject
{
    private ThemeManager()
    {
        Autodesk.AutoCAD.ApplicationServices.Core.Application.SystemVariableChanged += OnAutoCadThemeChanged;
    }

    #region AutoCadTheme

    private bool UseAutoCadTheme { get; set; }

    private static AppTheme GetAutoCadTheme()
    {
        var colorTheme = (short)Autodesk.AutoCAD.ApplicationServices.Core.Application.GetSystemVariable("COLORTHEME");
        return colorTheme == 0 ? AppTheme.Dark : AppTheme.Light;
    }

    private void OnAutoCadThemeChanged(object sender, Autodesk.AutoCAD.ApplicationServices.SystemVariableChangedEventArgs e)
    {
        if (!string.Equals(e.Name, "COLORTHEME", StringComparison.OrdinalIgnoreCase)) return;

        DispatcherHelper.RunOnMainThread(() =>
        {
            if (!UseAutoCadTheme) return;
            ActualApplicationTheme = GetAutoCadTheme();
            ApplyThemeToResources();
        });
    }

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
            UseAutoCadTheme = true;
            ActualApplicationTheme = GetAutoCadTheme();
        }
        else
        {
            UseAutoCadTheme = false;
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
