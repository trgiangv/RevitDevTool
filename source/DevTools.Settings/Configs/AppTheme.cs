namespace DevTools.Settings.Configs;

/// <summary>
/// Persisted application theme preference.
/// Ordinals must stay aligned with <c>DevTools.UI.Theme.AppTheme</c>
/// (<c>Light = 0</c>, <c>Dark = 1</c>, <c>Auto = 2</c>) because
/// <c>FileConfig</c> serializes enums as numbers.
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
    /// Automatically sync with the host application theme.
    /// </summary>
    Auto = 2
}
