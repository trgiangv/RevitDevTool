namespace DevTools.Settings;

/// <summary>
/// Configuration options for application paths.
/// Can be configured via services.Configure&lt;PathOptions&gt;().
/// </summary>
public sealed class PathOptions
{
    /// <summary>
    /// Directory for settings files.
    /// </summary>
    public string SettingsDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Directory for log files.
    /// </summary>
    public string LogsDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets the full path for a settings file.
    /// </summary>
    public string GetSettingsPath<T>() where T : class => Path.Combine(SettingsDirectory, $"{typeof(T).Name}.json");

    /// <summary>
    /// Ensures all directories exist. Called during configuration.
    /// </summary>
    public void EnsureDirectoriesExist()
    {
        TryCreateDirectory(SettingsDirectory);
        TryCreateDirectory(LogsDirectory);
    }

    private static void TryCreateDirectory(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path))
                Directory.CreateDirectory(path);
        }
        catch
        {
            // ignore
        }
    }
}
