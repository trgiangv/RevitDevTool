namespace AcadDevTool.Theme;

/// <summary>
/// Application theme enum that includes Auto option for AutoCAD theme sync.
/// </summary>
public enum AppTheme
{
    /// <summary>
    /// Use the Light default theme.
    /// </summary>
    Light = 0,

    /// <summary>
    /// Use the Dark default theme.
    /// </summary>
    Dark = 1,

    /// <summary>
    /// Automatically sync with AutoCAD theme.
    /// </summary>
    Auto = 2
}
