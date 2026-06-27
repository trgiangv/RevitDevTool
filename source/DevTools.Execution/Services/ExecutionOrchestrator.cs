using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Telemetry;
using DevTools.Utilities;
using Microsoft.Extensions.DependencyInjection;
namespace DevTools.Execution.Services;

/// <summary>
/// Main orchestrator that coordinates all services and providers.
/// </summary>
public sealed class ExecutionOrchestrator : IExecutionOrchestrator, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITreeStateManager _stateManager;
    private readonly IFileWatcherService _fileWatcher;
    private readonly ITelemetry _telemetry;
    private readonly ObservableCollection<ExecutionNodeBase> _treeRoot = [];
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private ExecutionNodeBase? _lastExecutedNode;

    public IEnumerable<ExecutionNodeBase> TreeRoot => _treeRoot;

    public event EventHandler? TreeChanged;
    public event EventHandler<RootRemovedEventArgs>? RootRemoved;
    public event EventHandler<ExecutionProgressEventArgs>? ExecutionProgressChanged;

    public ExecutionOrchestrator(
        IServiceProvider serviceProvider,
        ITreeStateManager stateManager,
        IFileWatcherService fileWatcher,
        ITelemetry telemetry)
    {
        _serviceProvider = serviceProvider;
        _stateManager = stateManager;
        _fileWatcher = fileWatcher;
        _telemetry = telemetry;
        _fileWatcher.FileChanged += OnFileChanged;
    }

    public async Task LoadFromPathAsync(string path, CancellationToken cancellationToken = default)
    {
        if (IsPathUnderExistingRoot(path))
        {
            Trace.TraceWarning($"Path '{path}' is already covered by an existing parent root. Skipping.");
            return;
        }

        AbsorbChildRoots(path);

        var providers = _serviceProvider.GetServices<IExecutionProvider>()
            .Where(p => p.CanHandle(path))
            .OrderByDescending(p => p.Priority)
            .ToList();

        if (providers.Count == 0)
            throw new ArgumentException($"No suitable provider found for path: {path}");

        var state = _stateManager.CaptureState(_treeRoot);
        var allWatchPatterns = new List<string>();

        foreach (var provider in providers)
        {
            if (!provider.ValidatePath(path)) continue;

            var discoveredNodes = await provider.DiscoverAsync(path, cancellationToken).ConfigureAwait(true);
            TreeNodeOperations.MergeNodesIntoTree(_treeRoot, discoveredNodes);
            allWatchPatterns.AddRange(provider.GetWatchPatterns());
        }

        _stateManager.RestoreState(_treeRoot, state, autoExpandNew: true);
        _fileWatcher.Watch(path, allWatchPatterns);
        TreeChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<IReadOnlyList<string>> LoadSavedPathsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        var failed = new List<string>();
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                failed.Add(path);
                continue;
            }

            try
            {
                await LoadFromPathAsync(path, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to load path '{path}': {ex.Message}");
                failed.Add(path);
                continue;
            }

            if (!HasRootForConfiguredPath(path))
            {
                failed.Add(path);
            }
        }

        return failed;
    }

    private bool HasRootForConfiguredPath(string path)
    {
        foreach (var root in _treeRoot.OfType<ExecutionNodeRoot>())
        {
            if (AreSamePath(path, root.RootPath))
                return true;
        }

        return false;
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _reloadGate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            if (_treeRoot.Count == 0)
                return;

            var state = _stateManager.CaptureState(_treeRoot);
            var previousExecutableIds = TreeNodeOperations.CollectExecutableIdSet(_treeRoot);
            var currentRoots = _treeRoot.OfType<ExecutionNodeRoot>().ToList();
            var reloadedRoots = new List<ExecutionNodeRoot>(currentRoots.Count);

            var removedRootPaths = new List<string>();

            foreach (var currentRoot in currentRoots)
            {
                var result = await ReloadRootAsync(currentRoot, cancellationToken).ConfigureAwait(true);

                if (result.RootNode != null)
                {
                    reloadedRoots.Add(result.RootNode);
                    continue;
                }

                if (result.KeepExistingRoot)
                {
                    reloadedRoots.Add(currentRoot);
                }
                else
                {
                    _fileWatcher.Unwatch(currentRoot.RootPath);
                    removedRootPaths.Add(currentRoot.RootPath);
                }
            }

            TreeNodeOperations.ReplaceRootSnapshot(_treeRoot, reloadedRoots);
            _stateManager.RestoreState(_treeRoot, state, autoExpandNew: false);
            _lastExecutedNode = TreeNodeOperations.PromoteLatestNewExecutable(_treeRoot, previousExecutableIds) ?? _lastExecutedNode;
            TreeChanged?.Invoke(this, EventArgs.Empty);

            foreach (var rootPath in removedRootPaths)
            {
                RootRemoved?.Invoke(this, new RootRemovedEventArgs(rootPath));
            }
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    public ExecutionNodeBase? RemoveNode(ExecutionNodeBase node)
    {
        var result = TreeNodeOperations.RemoveNodeWithCascade(_treeRoot, node, rootPath => _fileWatcher.Unwatch(rootPath));
        if (result.Removed)
            TreeChanged?.Invoke(this, EventArgs.Empty);

        return result.NextSelection;
    }

    public void ClearAll()
    {
        _treeRoot.Clear();
        _fileWatcher.UnwatchAll();
        TreeChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<ExecutionResult> ExecuteAsync(ExecutionNodeBase node, CancellationToken cancellationToken = default)
    {
        if (!node.IsExecutable)
            return ExecutionResult.Skipped();

        if (_lastExecutedNode != null && _lastExecutedNode != node)
            _lastExecutedNode.IsLastExecuted = false;

        var progress = new Progress<string>(message =>
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            ExecutionProgressChanged?.Invoke(this, new ExecutionProgressEventArgs(message));
        });

        var result = await node.ExecuteAsync(progress, cancellationToken);
        if (result.Success)
        {
            _lastExecutedNode = node;
        }

        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            ExecutionProgressChanged?.Invoke(this, new ExecutionProgressEventArgs(result.Message));
        }

        if (result.IsCancelled) return result;

        var providerKind = GetProviderKind(node);
        _telemetry.RecordExecutionInvocation(providerKind, result.Success);
        
        var isCritical = result is { Success: false, Exception: { } ex }
                         && TelemetryReporting.ShouldReportCriticalException(ex);
        if (isCritical)
        {
            _telemetry.RecordCriticalException(
                result.Exception!,
                TelemetryKeys.Feature.Execution,
                new Dictionary<string, string> { [TelemetryKeys.Tag.Provider] = providerKind });
        }

        return result;
    }

    public void Dispose()
    {
        _fileWatcher.FileChanged -= OnFileChanged;
        _fileWatcher.Dispose();
        _reloadGate.Dispose();
    }

    private bool IsPathUnderExistingRoot(string path)
    {
        return _treeRoot
            .OfType<ExecutionNodeRoot>()
            .Any(root => IsPathUnderRoot(path, root.RootPath));
    }

    private void AbsorbChildRoots(string path)
    {
        var childRoots = _treeRoot
            .OfType<ExecutionNodeRoot>()
            .Where(root => IsPathUnderRoot(root.RootPath, path) && !AreSamePath(root.RootPath, path))
            .ToList();

        foreach (var child in childRoots)
        {
            _fileWatcher.Unwatch(child.RootPath);
            TreeNodeOperations.RemoveNodeWithCascade(_treeRoot, child, _ => { });
            RootRemoved?.Invoke(this, new RootRemovedEventArgs(child.RootPath));
        }
    }

    private static string GetProviderKind(ExecutionNodeBase node) =>
        node is ExecutionNode executable ? executable.ExecutionMode.ToString() : "unknown";

    private async Task<ReloadRootResult> ReloadRootAsync(ExecutionNodeRoot currentExecutionNodeRoot, CancellationToken cancellationToken)
    {
        try
        {
            var provider = _serviceProvider.GetKeyedService<IExecutionProvider>(currentExecutionNodeRoot.ContainerMode);
            if (provider == null)
            {
                Trace.TraceWarning($"No keyed provider found for container mode '{currentExecutionNodeRoot.ContainerMode}'");
                return ReloadRootResult.KeepCurrent();
            }

            var nodes = await provider.DiscoverAsync(currentExecutionNodeRoot.RootPath, cancellationToken).ConfigureAwait(true);
            var discoveredRoot = nodes.OfType<ExecutionNodeRoot>().FirstOrDefault();
            return discoveredRoot != null
                ? ReloadRootResult.UseDiscovered(discoveredRoot)
                : ReloadRootResult.RemoveCurrent();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to reload path '{currentExecutionNodeRoot.RootPath}': {ex.Message}");
            return ReloadRootResult.KeepCurrent();
        }
    }

    private void OnFileChanged(object? sender, FileChangedEventArgs e)
    {
        if (e.ChangeType == FileChangeType.Modified)
            return;

        HostUiHelper.RunOnMainThread(async void () =>
        {
            try
            {
                await HandleFileChangeAsync(e).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error reloading after file change: {ex.Message}");
            }
        });
    }

    private async Task HandleFileChangeAsync(FileChangedEventArgs e)
    {
        await _reloadGate.WaitAsync().ConfigureAwait(true);
        try
        {
            var changed = e.Scope switch
            {
                FileWatcherScope.RootLifecycle => await HandleRootLifecycleEventAsync(e).ConfigureAwait(true),
                FileWatcherScope.FileContent or FileWatcherScope.DirectoryStructure => await ReloadAffectedRootAsync(e).ConfigureAwait(true),
                _ => false
            };

            if (changed)
            {
                TreeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    private async Task<bool> HandleRootLifecycleEventAsync(FileChangedEventArgs e)
    {
        if (e.ChangeType == FileChangeType.Renamed && !string.IsNullOrEmpty(e.OldPath))
        {
            return await HandleRootRenameAsync(e.OldPath!, e.Path).ConfigureAwait(true);
        }

        if (e.ChangeType == FileChangeType.Deleted)
        {
            var deletedRoot = FindRootByPath(e.Path);
            if (deletedRoot == null)
                return false;

            _fileWatcher.Unwatch(deletedRoot.RootPath);
            TreeNodeOperations.RemoveNodeWithCascade(_treeRoot, deletedRoot, _ => { });
            RootRemoved?.Invoke(this, new RootRemovedEventArgs(deletedRoot.RootPath));
            return true;
        }

        return false;
    }

    private async Task<bool> HandleRootRenameAsync(string oldPath, string newPath)
    {
        var renamedRoot = FindRootByPath(oldPath);

        if (renamedRoot == null)
        {
            return false;
        }

        _fileWatcher.Unwatch(oldPath);
        TreeNodeOperations.RemoveNodeWithCascade(_treeRoot, renamedRoot, _ => { });

        var provider = _serviceProvider.GetKeyedService<IExecutionProvider>(renamedRoot.ContainerMode);
        if (provider == null)
        {
            Trace.TraceWarning($"No keyed provider found for container mode '{renamedRoot.ContainerMode}'");
            RootRemoved?.Invoke(this, new RootRemovedEventArgs(oldPath));
            return true;
        }

        var loadedRenamedRoot = false;
        if (provider.ValidatePath(newPath))
        {
            var discoveredNodes = await provider.DiscoverAsync(newPath).ConfigureAwait(true);
            var incomingNodes = discoveredNodes as ExecutionNodeBase[] ?? discoveredNodes.ToArray();
            TreeNodeOperations.MergeNodesIntoTree(_treeRoot, incomingNodes);
            _fileWatcher.Watch(newPath, provider.GetWatchPatterns());
            loadedRenamedRoot = incomingNodes.OfType<ExecutionNodeRoot>().Any();
        }

        RootRemoved?.Invoke(this, loadedRenamedRoot
            ? new RootRemovedEventArgs(oldPath, newPath)
            : new RootRemovedEventArgs(oldPath));
        return true;
    }

    private async Task<bool> ReloadAffectedRootAsync(FileChangedEventArgs e)
    {
        var affectedRoot = FindAffectedRoot(e);
        if (affectedRoot == null)
            return false;

        var state = _stateManager.CaptureState(_treeRoot);
        var previousExecutableIds = TreeNodeOperations.CollectExecutableIdSet(_treeRoot);

        var result = await ReloadRootAsync(affectedRoot, CancellationToken.None).ConfigureAwait(true);
        var changed = ApplyRootReloadResult(affectedRoot, result);

        if (!changed)
            return false;

        _stateManager.RestoreState(_treeRoot, state, autoExpandNew: false);
        _lastExecutedNode = TreeNodeOperations.PromoteLatestNewExecutable(_treeRoot, previousExecutableIds) ?? _lastExecutedNode;
        return true;
    }

    private bool ApplyRootReloadResult(ExecutionNodeRoot currentExecutionNodeRoot, ReloadRootResult result)
    {
        if (result.RootNode == null && result.KeepExistingRoot)
            return false;

        var rootIndex = _treeRoot.IndexOf(currentExecutionNodeRoot);
        if (rootIndex < 0)
            return false;

        if (result.RootNode != null)
        {
            _treeRoot[rootIndex] = result.RootNode;
            return true;
        }

        _fileWatcher.Unwatch(currentExecutionNodeRoot.RootPath);
        _treeRoot.RemoveAt(rootIndex);
        RootRemoved?.Invoke(this, new RootRemovedEventArgs(currentExecutionNodeRoot.RootPath));
        return true;
    }

    private ExecutionNodeRoot? FindAffectedRoot(FileChangedEventArgs e)
    {
        return _treeRoot
            .OfType<ExecutionNodeRoot>()
            .FirstOrDefault(root =>
                IsPathUnderRoot(e.Path, root.RootPath) ||
                !string.IsNullOrEmpty(e.OldPath) && IsPathUnderRoot(e.OldPath!, root.RootPath));
    }

    private ExecutionNodeRoot? FindRootByPath(string path)
    {
        return _treeRoot
            .OfType<ExecutionNodeRoot>()
            .FirstOrDefault(root => AreSamePath(root.RootPath, path));
    }

    private static bool IsPathUnderRoot(string path, string rootPath)
    {
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
               || normalizedPath.StartsWith($"{normalizedRoot}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
               || normalizedPath.StartsWith($"{normalizedRoot}{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool AreSamePath(string left, string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct ReloadRootResult(ExecutionNodeRoot? RootNode, bool KeepExistingRoot)
    {
        public static ReloadRootResult UseDiscovered(ExecutionNodeRoot executionNodeRoot) => new(executionNodeRoot, false);
        public static ReloadRootResult KeepCurrent() => new(null, true);
        public static ReloadRootResult RemoveCurrent() => new(null, false);
    }
}