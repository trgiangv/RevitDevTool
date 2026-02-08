using System.Collections.Concurrent;
using System.IO;
using RevitDevTool.CodeExecute.Interfaces;

namespace RevitDevTool.CodeExecute.Services;

/// <summary>
/// Implementation of IFileWatcherService.
/// Watches file system changes with debouncing.
/// </summary>
public sealed class FileWatcherService : IFileWatcherService
{
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, System.Threading.Timer> _debounceTimers = new();
    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(500);
    private bool _disposed;

    public event EventHandler<FileChangedEventArgs>? FileChanged;

    public void Watch(string path, IEnumerable<string> patterns)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileWatcherService));
        if (string.IsNullOrWhiteSpace(path)) return;

        var directoryPath = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            return;

        // Stop existing watcher if any
        Unwatch(path);

        foreach (var pattern in patterns)
        {
            var key = $"{directoryPath}|{pattern}";
            var watcher = new FileSystemWatcher(directoryPath, pattern)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            watcher.Changed += OnFileSystemChanged;
            watcher.Created += OnFileSystemChanged;
            watcher.Deleted += OnFileSystemChanged;
            watcher.Renamed += OnFileSystemRenamed;

            _watchers[key] = watcher;
        }
    }

    public void Unwatch(string path)
    {
        if (_disposed) return;

        var keysToRemove = _watchers.Keys.Where(k => k.StartsWith(path, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var key in keysToRemove)
        {
            if (_watchers.TryRemove(key, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            if (_debounceTimers.TryRemove(key, out var timer))
            {
                timer.Dispose();
            }
        }
    }

    public void UnwatchAll()
    {
        if (_disposed) return;

        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();

        foreach (var timer in _debounceTimers.Values)
        {
            timer.Dispose();
        }
        _debounceTimers.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        UnwatchAll();
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        DebouncedRaiseEvent(e.FullPath, MapChangeType(e.ChangeType));
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        DebouncedRaiseEvent(e.FullPath, FileChangeType.Renamed);
    }

    private void DebouncedRaiseEvent(string path, FileChangeType changeType)
    {
        var key = path;

        // Cancel existing timer if any
        if (_debounceTimers.TryRemove(key, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        var timer = new System.Threading.Timer(_ =>
        {
            FileChanged?.Invoke(this, new FileChangedEventArgs
            {
                Path = path,
                ChangeType = changeType
            });

            if (_debounceTimers.TryRemove(key, out var removedTimer))
            {
                removedTimer.Dispose();
            }
        }, null, _debounceDelay, Timeout.InfiniteTimeSpan);

        _debounceTimers[key] = timer;
    }

    private static FileChangeType MapChangeType(WatcherChangeTypes changeType)
    {
        return changeType switch
        {
            WatcherChangeTypes.Created => FileChangeType.Created,
            WatcherChangeTypes.Deleted => FileChangeType.Deleted,
            WatcherChangeTypes.Changed => FileChangeType.Modified,
            WatcherChangeTypes.Renamed => FileChangeType.Renamed,
            _ => FileChangeType.Modified
        };
    }
}