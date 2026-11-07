using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Settings;
using DevTools.Telemetry;
using DevTools.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
using ZLogger;
namespace DevTools.Execution.Services;

/// <summary>
/// Owns Command Execution: the root tree, its saved paths, file watching, and execution.
/// </summary>
public sealed class ExecutionOrchestrator : IExecutionOrchestrator, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITreeStateManager _stateManager;
    private readonly IFileWatcherService _fileWatcher;
    private readonly ISettingsService _settingsService;
    private readonly ITelemetry _telemetry;
    private readonly ILogger<ExecutionOrchestrator> _logger;
    private readonly ObservableCollection<ExecutionNodeBase> _treeRoot = [];
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private Task? _savedPathsLoad;

    public IEnumerable<ExecutionNodeBase> TreeRoot => _treeRoot;

    public event EventHandler? TreeChanged;
    public event EventHandler<ExecutionProgressEventArgs>? ExecutionProgressChanged;

    public ExecutionOrchestrator(
        IServiceProvider serviceProvider,
        ITreeStateManager stateManager,
        IFileWatcherService fileWatcher,
        ISettingsService settingsService,
        ITelemetry telemetry,
        ILogger<ExecutionOrchestrator> logger)
    {
        _serviceProvider = serviceProvider;
        _stateManager = stateManager;
        _fileWatcher = fileWatcher;
        _settingsService = settingsService;
        _telemetry = telemetry;
        _logger = logger;
        _fileWatcher.FileChanged += OnFileChanged;
    }

    public Task LoadSavedPathsAsync(CancellationToken cancellationToken = default) =>
        _savedPathsLoad ??= LoadSavedPathsCoreAsync();

    private async Task LoadSavedPathsCoreAsync()
    {
        var config = _settingsService.ExecutionConfig;
        var paths = config.DotnetAssemblyPaths.Concat(config.ScriptFolderPaths).ToList();
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                ForgetPath(path);
                continue;
            }

            try
            {
                await LoadFromPathAsync(path).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.ZLogError($"{ex.Message}");
            }

            if (FindRoot(path) is null)
                ForgetPath(path);
        }
    }

    public async Task LoadFromPathAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        var existing = FindRoot(path);
        if (existing is not null)
        {
            if (!AreSamePath(existing.RootPath, path))
                _logger.ZLogWarning($"Path '{path}' is already covered by root '{existing.RootPath}'. Skipping.");
            return;
        }

        var providers = _serviceProvider.GetServices<IExecutionProvider>()
            .Where(p => p.CanHandle(path))
            .OrderByDescending(p => p.Priority)
            .ToList();

        if (providers.Count == 0)
            throw new ArgumentException($"No suitable provider found or path not exist: {path}");

        AbsorbChildRoots(path);

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

        if (FindRoot(path) is { } root)
            RememberPath(root.RootPath, root.ContainerMode);

        TreeChanged?.Invoke(this, EventArgs.Empty);
    }

    public ExecutionNodeRoot? FindRoot(string path) =>
        _treeRoot.OfType<ExecutionNodeRoot>().FirstOrDefault(root => AreSamePath(root.RootPath, path))
        ?? _treeRoot.OfType<ExecutionNodeRoot>().FirstOrDefault(root => CoversAsScriptRoot(root, path));

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

            foreach (var currentRoot in currentRoots)
            {
                var result = await DiscoverRootAsync(currentRoot, cancellationToken).ConfigureAwait(true);

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
                    ForgetPath(currentRoot.RootPath);
                }
            }

            TreeNodeOperations.ReplaceRootSnapshot(_treeRoot, reloadedRoots);
            _stateManager.RestoreState(_treeRoot, state, autoExpandNew: false);
            TreeNodeOperations.PromoteLatestNewExecutable(_treeRoot, previousExecutableIds);
            TreeChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    public async Task<ExecutionNodeRoot?> ReloadRootAsync(ExecutionNodeRoot root, CancellationToken cancellationToken = default)
    {
        await _reloadGate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            if (await ReloadRootCoreAsync(root, cancellationToken).ConfigureAwait(true))
                TreeChanged?.Invoke(this, EventArgs.Empty);

            return _treeRoot.OfType<ExecutionNodeRoot>().FirstOrDefault(r => AreSamePath(r.RootPath, root.RootPath));
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    public ExecutionNodeBase? RemoveNode(ExecutionNodeBase node)
    {
        var result = TreeNodeOperations.RemoveNodeWithCascade(_treeRoot, node, rootPath =>
        {
            _fileWatcher.Unwatch(rootPath);
            ForgetPath(rootPath);
        });
        if (result.Removed)
            TreeChanged?.Invoke(this, EventArgs.Empty);

        return result.NextSelection;
    }

    public void ClearAll()
    {
        _treeRoot.Clear();
        _fileWatcher.UnwatchAll();
        var config = _settingsService.ExecutionConfig;
        config.DotnetAssemblyPaths.Clear();
        config.ScriptFolderPaths.Clear();
        PersistPaths();
        TreeChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<ExecutionResult> ExecuteAsync(ExecutionNodeBase node, CancellationToken cancellationToken = default)
    {
        if (!node.IsExecutable)
            return ExecutionResult.Skipped();

        TreeNodeOperations.MarkLastExecuted(_treeRoot, node);

        var progress = new Progress<string>(message =>
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            ExecutionProgressChanged?.Invoke(this, new ExecutionProgressEventArgs(message));
        });

        var providerKind = GetProviderKind(node);
        using var operation = _telemetry.StartOperation(TelemetryKeys.Operation.Execution, providerKind);

        var result = await node.ExecuteAsync(progress, cancellationToken);

        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            ExecutionProgressChanged?.Invoke(this, new ExecutionProgressEventArgs(result.Message));
        }

        if (result.Success)
            operation.Complete();
        else if (result.IsCancelled)
            operation.Cancel();
        else
            operation.Fail(result.Exception);

        return result;
    }

    public void Dispose()
    {
        _fileWatcher.FileChanged -= OnFileChanged;
        _fileWatcher.Dispose();
        _reloadGate.Dispose();
    }

    private void RememberPath(string path, ContainerMode container)
    {
        var config = _settingsService.ExecutionConfig;
        var list = container == ContainerMode.Assembly ? config.DotnetAssemblyPaths : config.ScriptFolderPaths;
        if (list.Contains(path, StringComparer.OrdinalIgnoreCase))
            return;

        list.Add(path);
        PersistPaths();
    }

    private void ForgetPath(string path)
    {
        var config = _settingsService.ExecutionConfig;
        var removed = config.DotnetAssemblyPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase))
                      + config.ScriptFolderPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
            PersistPaths();
    }

    private void PersistPaths()
    {
        try
        {
            _settingsService.SaveSettings();
        }
        catch (Exception ex)
        {
            _logger.ZLogWarning(ex, $"Saving Command Execution paths failed: {ex.Message}");
        }
    }

    private void AbsorbChildRoots(string path)
    {
        if (!Directory.Exists(path))
            return;

        var childRoots = _treeRoot
            .OfType<ExecutionNodeRoot>()
            .Where(root => root.ContainerMode == ContainerMode.Script
                           && !AreSamePath(root.RootPath, path)
                           && IsPathUnderRoot(root.RootPath, path))
            .ToList();

        foreach (var child in childRoots)
        {
            _fileWatcher.Unwatch(child.RootPath);
            TreeNodeOperations.RemoveNodeWithCascade(_treeRoot, child, _ => { });
            ForgetPath(child.RootPath);
        }
    }

    private static string GetProviderKind(ExecutionNodeBase node) =>
        node is ExecutionNode executable ? executable.ExecutionMode.ToString() : "unknown";

    private async Task<ReloadRootResult> DiscoverRootAsync(ExecutionNodeRoot currentExecutionNodeRoot, CancellationToken cancellationToken)
    {
        try
        {
            var provider = _serviceProvider.GetKeyedService<IExecutionProvider>(currentExecutionNodeRoot.ContainerMode);
            if (provider == null)
            {
                _logger.ZLogWarning($"No keyed provider found for container mode '{currentExecutionNodeRoot.ContainerMode}'");
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
            _logger.ZLogError($"Failed to reload path '{currentExecutionNodeRoot.RootPath}': {ex.Message}");
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
                _logger.ZLogError($"Error reloading after file change: {ex.Message}");
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
            var deletedRoot = FindExactRoot(e.Path);
            if (deletedRoot == null)
                return false;

            _fileWatcher.Unwatch(deletedRoot.RootPath);
            TreeNodeOperations.RemoveNodeWithCascade(_treeRoot, deletedRoot, _ => { });
            ForgetPath(deletedRoot.RootPath);
            return true;
        }

        return false;
    }

    private async Task<bool> HandleRootRenameAsync(string oldPath, string newPath)
    {
        var renamedRoot = FindExactRoot(oldPath);

        if (renamedRoot == null)
        {
            return false;
        }

        _fileWatcher.Unwatch(oldPath);
        TreeNodeOperations.RemoveNodeWithCascade(_treeRoot, renamedRoot, _ => { });
        ForgetPath(oldPath);

        var provider = _serviceProvider.GetKeyedService<IExecutionProvider>(renamedRoot.ContainerMode);
        if (provider == null)
        {
            _logger.ZLogWarning($"No keyed provider found for container mode '{renamedRoot.ContainerMode}'");
            return true;
        }

        if (provider.ValidatePath(newPath))
        {
            var discoveredNodes = await provider.DiscoverAsync(newPath).ConfigureAwait(true);
            var incomingNodes = discoveredNodes as ExecutionNodeBase[] ?? discoveredNodes.ToArray();
            TreeNodeOperations.MergeNodesIntoTree(_treeRoot, incomingNodes);
            _fileWatcher.Watch(newPath, provider.GetWatchPatterns());
            if (incomingNodes.OfType<ExecutionNodeRoot>().Any())
                RememberPath(newPath, renamedRoot.ContainerMode);
        }

        return true;
    }

    private Task<bool> ReloadAffectedRootAsync(FileChangedEventArgs e)
    {
        var affectedRoot = FindAffectedRoot(e);
        return affectedRoot == null
            ? Task.FromResult(false)
            : ReloadRootCoreAsync(affectedRoot, CancellationToken.None);
    }

    private async Task<bool> ReloadRootCoreAsync(ExecutionNodeRoot root, CancellationToken cancellationToken)
    {
        var state = _stateManager.CaptureState(_treeRoot);
        var previousExecutableIds = TreeNodeOperations.CollectExecutableIdSet(_treeRoot);

        var result = await DiscoverRootAsync(root, cancellationToken).ConfigureAwait(true);
        if (!ApplyRootReloadResult(root, result))
            return false;

        _stateManager.RestoreState(_treeRoot, state, autoExpandNew: false);
        TreeNodeOperations.PromoteLatestNewExecutable(_treeRoot, previousExecutableIds);
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
        ForgetPath(currentExecutionNodeRoot.RootPath);
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

    private ExecutionNodeRoot? FindExactRoot(string path)
    {
        return _treeRoot
            .OfType<ExecutionNodeRoot>()
            .FirstOrDefault(root => AreSamePath(root.RootPath, path));
    }

    private static bool CoversAsScriptRoot(ExecutionNodeRoot root, string path) =>
        root.ContainerMode == ContainerMode.Script
        && !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        && IsPathUnderRoot(path, root.RootPath);

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
