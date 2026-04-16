namespace RevitDevTool.Execution.Interfaces;

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
    /// Path that changed (new path for Renamed)
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Previous path before rename (only set for Renamed events)
    /// </summary>
    public string? OldPath { get; init; }

    /// <summary>
    /// Type of change (Created, Modified, Deleted, Renamed)
    /// </summary>
    public required FileChangeType ChangeType { get; init; }

    /// <summary>
    /// Which watcher raised this event.
    /// Used by orchestrator to route reload logic without guessing by path.
    /// </summary>
    public required FileWatcherScope Scope { get; init; }
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

public enum FileWatcherScope
{
    /// <summary>
    /// File pattern watcher (*.py, *.fsx, *.dll)
    /// </summary>
    FileContent,

    /// <summary>
    /// Intermediate folder create/rename/delete inside a watched root.
    /// </summary>
    DirectoryStructure,

    /// <summary>
    /// Root folder lifecycle changes from parent folder (rename/delete).
    /// </summary>
    RootLifecycle
}