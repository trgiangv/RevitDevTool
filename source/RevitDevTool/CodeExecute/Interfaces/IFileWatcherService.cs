namespace RevitDevTool.CodeExecute.Interfaces;

/// <summary>
/// Service for watching file system changes and triggering auto-reload.
/// Handles debouncing and pattern-based watching.
/// </summary>
public interface IFileWatcherService : IDisposable
{
    /// <summary>
    /// Event raised when watched files change
    /// </summary>
    event EventHandler<FileChangedEventArgs>? FileChanged;

    /// <summary>
    /// Start watching a path with specific patterns
    /// </summary>
    /// <param name="path">Path to watch (file or directory)</param>
    /// <param name="patterns">File patterns to watch (e.g., "*.dll", "*.py")</param>
    void Watch(string path, IEnumerable<string> patterns);

    /// <summary>
    /// Stop watching a specific path
    /// </summary>
    /// <param name="path">Path to stop watching</param>
    void Unwatch(string path);

    /// <summary>
    /// Stop watching all paths
    /// </summary>
    void UnwatchAll();
}

/// <summary>
/// Event arguments for file change events
/// </summary>
public sealed class FileChangedEventArgs : EventArgs
{
    /// <summary>
    /// Path that changed
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Type of change (Created, Modified, Deleted, Renamed)
    /// </summary>
    public required FileChangeType ChangeType { get; init; }
}

/// <summary>
/// Type of file system change
/// </summary>
public enum FileChangeType
{
    Created,
    Modified,
    Deleted,
    Renamed
}