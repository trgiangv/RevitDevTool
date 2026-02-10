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
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _registeredWatchers = new();
    private readonly ConcurrentDictionary<string, System.Threading.Timer> _notificationTimers = new();
    private readonly ConcurrentDictionary<string, FileChangeType> _pendingChanges = new();
    private readonly TimeSpan _notificationDelay = TimeSpan.FromMilliseconds(500);
    private bool _disposed;

    public event EventHandler<FileChangedEventArgs>? FileChanged;

    public void Watch(string path, IEnumerable<string> patterns)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileWatcherService));
        if (string.IsNullOrWhiteSpace(path)) return;

        var directoryPath = ResolveDirectoryPath(path);
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            return;

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

            watcher.Changed += HandleFileSystemChanged;
            watcher.Created += HandleFileSystemChanged;
            watcher.Deleted += HandleFileSystemChanged;
            watcher.Renamed += HandleFileSystemRenamed;

            _registeredWatchers[key] = watcher;
        }
    }

    public void Unwatch(string path)
    {
        if (_disposed) return;

        var directoryPath = ResolveDirectoryPath(path);
        if (string.IsNullOrEmpty(directoryPath))
            return;

        var watchKeyPrefix = $"{directoryPath}|";
        var keysToRemove = _registeredWatchers.Keys
            .Where(k => k.StartsWith(watchKeyPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            if (_registeredWatchers.TryRemove(key, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            if (_notificationTimers.TryRemove(key, out var timer))
            {
                timer.Dispose();
            }
        }
    }

    public void UnwatchAll()
    {
        if (_disposed) return;

        foreach (var watcher in _registeredWatchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _registeredWatchers.Clear();

        foreach (var timer in _notificationTimers.Values)
        {
            timer.Dispose();
        }
        _notificationTimers.Clear();
        _pendingChanges.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;

        UnwatchAll();
        _disposed = true;
    }

    private void HandleFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        ScheduleChangeNotification(e.FullPath, MapWatcherChangeType(e.ChangeType));
    }

    private void HandleFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        ScheduleChangeNotification(e.FullPath, FileChangeType.Renamed);
    }

    private void ScheduleChangeNotification(string path, FileChangeType changeType)
    {
        var key = path;
        _pendingChanges.AddOrUpdate(key, changeType, (_, existing) => MergePendingChangeType(existing, changeType));

        if (_notificationTimers.TryRemove(key, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        var timer = new System.Threading.Timer(_ =>
        {
            if (_pendingChanges.TryRemove(key, out var finalChangeType))
            {
                FileChanged?.Invoke(this, new FileChangedEventArgs
                {
                    Path = path,
                    ChangeType = finalChangeType
                });
            }

            if (_notificationTimers.TryRemove(key, out var removedTimer))
            {
                removedTimer.Dispose();
            }
        }, null, _notificationDelay, Timeout.InfiniteTimeSpan);

        _notificationTimers[key] = timer;
    }

    private static FileChangeType MapWatcherChangeType(WatcherChangeTypes changeType)
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

    private static string? ResolveDirectoryPath(string path)
    {
        if (Directory.Exists(path))
            return path;

        return Path.GetDirectoryName(path);
    }

    private static bool IsStructuralFileChange(FileChangeType changeType)
    {
        return changeType != FileChangeType.Modified;
    }

    private static FileChangeType MergePendingChangeType(FileChangeType existing, FileChangeType incoming)
    {
        if (IsStructuralFileChange(existing) && incoming == FileChangeType.Modified)
            return existing;

        return incoming;
    }
}