using System.IO;

namespace RevitDevTool.Services.Configuration;

/// <summary>
/// Configuration options for application paths.
/// Can be configured via services.Configure&lt;PathOptions&gt;().
/// </summary>
public sealed class PathOptions
{
    /// <summary>
    /// Base directory for all application data (settings, logs, etc.)
    /// </summary>
    [UsedImplicitly]
    public string RootDirectory { get; set; } = string.Empty;

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
    /// Gets the default log file path with the specified extension.
    /// </summary>
    public string GetDefaultLogPath(string extension) => Path.Combine(LogsDirectory, $"log.{extension}");

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
