namespace RevitDevTool.Theme;

/// <summary>
/// Application theme enumeration for WinForms integration
/// </summary>
public enum ApplicationTheme
{
    Unknown,
    Light,
    Dark,
    HighContrast
}

/// <summary>
/// Simplified theme manager for WinForms integration
/// </summary>
public static class ApplicationThemeManager
{
    private static ApplicationTheme _currentTheme = ApplicationTheme.Light;
    
    /// <summary>
    /// Event triggered when the application's theme is changed.
    /// </summary>
    public static event Action<ApplicationTheme, System.Windows.Media.Color>? Changed;

    /// <summary>
    /// Gets the current application theme.
    /// </summary>
    public static ApplicationTheme GetAppTheme() => _currentTheme;

    /// <summary>
    /// Applies the specified theme.
    /// </summary>
    public static void Apply(ApplicationTheme theme)
    {
        if (_currentTheme == theme) return;
        
        _currentTheme = theme;
        Changed?.Invoke(theme, System.Windows.Media.Colors.Blue);
    }

    /// <summary>
    /// Applies the system theme.
    /// </summary>
    public static void ApplySystemTheme(bool updateAccents = true)
    {
        var systemTheme = GetSystemTheme();
        var appTheme = systemTheme switch
        {
            SystemTheme.Light => ApplicationTheme.Light,
            SystemTheme.Dark => ApplicationTheme.Dark,
            SystemTheme.HighContrast => ApplicationTheme.HighContrast,
            _ => ApplicationTheme.Light
        };
        
        Apply(appTheme);
    }

    /// <summary>
    /// Gets the current system theme.
    /// </summary>
    public static SystemTheme GetSystemTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            
            if (value is int intValue)
            {
                return intValue == 0 ? SystemTheme.Dark : SystemTheme.Light;
            }
        }
        catch
        {
            // Fallback to light theme if registry access fails
        }
        
        return SystemTheme.Light;
    }
}

/// <summary>
/// System theme enumeration
/// </summary>
public enum SystemTheme
{
    Unknown,
    Light,
    Dark,
    HighContrast,
    HC1,
    HC2,
    HCBlack,
    HCWhite
}
