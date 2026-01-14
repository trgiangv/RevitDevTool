namespace RevitDevTool.Theme;

/// <summary>
/// Application theme enum that includes Auto option for Revit theme sync.
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
    /// Automatically sync with Revit theme (only available in Revit 2024+).
    /// </summary>
    Auto = 2
}
