using System.Collections.Concurrent;
using System.IO;
using RevitDevTool.Execution.Interfaces;
namespace RevitDevTool.Execution.Services;

/// <summary>
/// Watches file system changes with debouncing.
/// Two layers of watchers:
///   - File watchers: track script file changes inside the root (*.py, *.fsx, *.dll)
///   - Parent watcher: track rename/delete of the root folder itself from its parent directory
/// </summary>
public sealed class FileWatcherService : IFileWatcherService
{
    private const string ParentWatcherSuffix = "|__parent__";
    private const string DirectoryWatcherSuffix = "|__dir__";

    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, System.Threading.Timer> _debounceTimers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PendingChange> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(500);
    private bool _disposed;

    public event EventHandler<FileChangedEventArgs>? FileChanged;

    public void Watch(string path, IEnumerable<string> patterns)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileWatcherService));
        if (string.IsNullOrWhiteSpace(path)) return;

        var rootPath = NormalizePath(path);
        if (string.IsNullOrEmpty(rootPath))
            return;

        var directoryPath = ResolveDirectoryPath(rootPath!);
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            return;

        Unwatch(rootPath!);

        foreach (var pattern in patterns)
        {
            var key = $"{rootPath}|{pattern}";
            var watcher = new FileSystemWatcher(directoryPath, pattern)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            watcher.Changed += OnFileContentChanged;
            watcher.Created += OnFileContentChanged;
            watcher.Deleted += OnFileContentChanged;
            watcher.Renamed += OnFileContentRenamed;

            _watchers[key] = watcher;
        }

        WatchRootLifecycle(rootPath!);
        WatchDirectoryStructure(rootPath!);
    }

    public void Unwatch(string path)
    {
        if (_disposed) return;

        var rootPath = NormalizePath(path);
        if (string.IsNullOrEmpty(rootPath))
            return;

        var prefix = $"{rootPath}|";

        var keysToRemove = _watchers.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        || k.Equals($"{rootPath}{ParentWatcherSuffix}", StringComparison.OrdinalIgnoreCase)
                        || k.Equals($"{rootPath}{DirectoryWatcherSuffix}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            DisposeWatcher(key);
        }

        var debounceKeysToRemove = _debounceTimers.Keys
            .Where(k => IsDebounceKeyUnderRoot(k, rootPath!))
            .ToList();

        foreach (var debounceKey in debounceKeysToRemove)
        {
            if (_debounceTimers.TryRemove(debounceKey, out var timer))
            {
                timer.Dispose();
            }

            _pendingChanges.TryRemove(debounceKey, out _);
        }
    }

    public void UnwatchAll()
    {
        if (_disposed) return;

        foreach (var key in _watchers.Keys.ToList())
        {
            DisposeWatcher(key);
        }

        _pendingChanges.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        UnwatchAll();
        _disposed = true;
    }

    #region File content watchers

    private void OnFileContentChanged(object sender, FileSystemEventArgs e)
    {
        ScheduleDebouncedNotification(
            path: e.FullPath,
            changeType: MapChangeType(e.ChangeType),
            scope: FileWatcherScope.FileContent);
    }

    private void OnFileContentRenamed(object sender, RenamedEventArgs e)
    {
        ScheduleDebouncedNotification(
            path: e.FullPath,
            changeType: FileChangeType.Renamed,
            scope: FileWatcherScope.FileContent,
            oldPath: e.OldFullPath);
    }

    #endregion

    #region Root lifecycle watcher

    private void WatchRootLifecycle(string rootPath)
    {
        var parentDir = Path.GetDirectoryName(rootPath);
        if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir))
            return;

        var itemName = Path.GetFileName(rootPath);
        if (string.IsNullOrEmpty(itemName))
            return;

        var parentKey = $"{rootPath}{ParentWatcherSuffix}";
        var watcher = new FileSystemWatcher(parentDir, itemName)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        watcher.Deleted += OnRootDeleted;
        watcher.Renamed += OnRootRenamed;

        _watchers[parentKey] = watcher;
    }

    private void OnRootDeleted(object sender, FileSystemEventArgs e)
    {
        FireImmediate(new FileChangedEventArgs
        {
            Path = e.FullPath,
            ChangeType = FileChangeType.Deleted,
            Scope = FileWatcherScope.RootLifecycle
        });
    }

    private void OnRootRenamed(object sender, RenamedEventArgs e)
    {
        FireImmediate(new FileChangedEventArgs
        {
            Path = e.FullPath,
            OldPath = e.OldFullPath,
            ChangeType = FileChangeType.Renamed,
            Scope = FileWatcherScope.RootLifecycle
        });
    }

    private void FireImmediate(FileChangedEventArgs args)
    {
        FileChanged?.Invoke(this, args);
    }

    #endregion

    #region Intermediate directory watcher

    private void WatchDirectoryStructure(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            return;

        var key = $"{rootPath}{DirectoryWatcherSuffix}";
        var watcher = new FileSystemWatcher(rootPath, "*")
        {
            NotifyFilter = NotifyFilters.DirectoryName,
            EnableRaisingEvents = true,
            IncludeSubdirectories = true
        };

        watcher.Created += OnDirectoryStructureChanged;
        watcher.Deleted += OnDirectoryStructureChanged;
        watcher.Renamed += OnDirectoryStructureRenamed;

        _watchers[key] = watcher;
    }

    private void OnDirectoryStructureChanged(object sender, FileSystemEventArgs e)
    {
        ScheduleDebouncedNotification(
            path: e.FullPath,
            changeType: MapChangeType(e.ChangeType),
            scope: FileWatcherScope.DirectoryStructure);
    }

    private void OnDirectoryStructureRenamed(object sender, RenamedEventArgs e)
    {
        ScheduleDebouncedNotification(
            path: e.FullPath,
            changeType: FileChangeType.Renamed,
            scope: FileWatcherScope.DirectoryStructure,
            oldPath: e.OldFullPath);
    }

    #endregion

    #region Debounce

    private void ScheduleDebouncedNotification(string path, FileChangeType changeType, FileWatcherScope scope, string? oldPath = null)
    {
        var key = BuildDebounceKey(scope, path);
        var incoming = new PendingChange(path, oldPath, changeType, scope);
        _pendingChanges.AddOrUpdate(key, incoming, (_, existing) => MergeChange(existing, incoming));

        if (_debounceTimers.TryRemove(key, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        var timer = new System.Threading.Timer(_ =>
        {
            if (_pendingChanges.TryRemove(key, out var finalChange))
            {
                FileChanged?.Invoke(this, new FileChangedEventArgs
                {
                    Path = finalChange.Path,
                    OldPath = finalChange.OldPath,
                    ChangeType = finalChange.ChangeType,
                    Scope = finalChange.Scope
                });
            }

            if (_debounceTimers.TryRemove(key, out var t))
            {
                t.Dispose();
            }
        }, null, _debounceDelay, Timeout.InfiniteTimeSpan);

        _debounceTimers[key] = timer;
    }

    private static PendingChange MergeChange(PendingChange existing, PendingChange incoming)
    {
        if (existing.ChangeType != FileChangeType.Modified && incoming.ChangeType == FileChangeType.Modified)
            return existing;

        return incoming with
        {
            OldPath = incoming.OldPath ?? existing.OldPath
        };
    }

    #endregion

    #region Helpers

    private void DisposeWatcher(string key)
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

    private static string BuildDebounceKey(FileWatcherScope scope, string path)
    {
        return $"{scope}|{path}";
    }

    private static bool IsDebounceKeyUnderRoot(string debounceKey, string rootPath)
    {
        var separatorIndex = debounceKey.IndexOf('|');
        if (separatorIndex < 0 || separatorIndex >= debounceKey.Length - 1)
            return false;

        var eventPath = debounceKey[(separatorIndex + 1)..];
        return IsPathUnderRoot(eventPath, rootPath);
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

    private static string? ResolveDirectoryPath(string path)
    {
        return Directory.Exists(path) ? path : Path.GetDirectoryName(path);
    }

    private static string? NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : 
#if NET
            Path.TrimEndingDirectorySeparator(path);
#else
            Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
#endif
    }

    private static bool IsPathUnderRoot(string path, string rootPath)
    {
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
               || normalizedPath.StartsWith($"{normalizedRoot}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
               || normalizedPath.StartsWith($"{normalizedRoot}{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct PendingChange(string Path, string? OldPath, FileChangeType ChangeType, FileWatcherScope Scope);

    #endregion
}
