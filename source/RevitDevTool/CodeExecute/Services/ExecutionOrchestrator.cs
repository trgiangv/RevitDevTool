using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Diagnostics;
using RevitDevTool.CodeExecute.Interfaces;
using RevitDevTool.CodeExecute.Models;

namespace RevitDevTool.CodeExecute.Services;

/// <summary>
/// Main orchestrator that coordinates all services and providers.
/// </summary>
public sealed class ExecutionOrchestrator : IExecutionOrchestrator, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITreeStateManager _stateManager;
    private readonly IFileWatcherService _fileWatcher;
    private readonly ObservableCollection<BaseNode> _treeRoot = [];
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private BaseNode? _lastExecutedNode;

    public IEnumerable<BaseNode> TreeRoot => _treeRoot;

    public event EventHandler? TreeChanged;

    public ExecutionOrchestrator(IServiceProvider serviceProvider, ITreeStateManager stateManager, IFileWatcherService fileWatcher)
    {
        _serviceProvider = serviceProvider;
        _stateManager = stateManager;
        _fileWatcher = fileWatcher;
        _fileWatcher.FileChanged += OnFileChanged;
    }

    public async Task LoadFromPathAsync(string path, CancellationToken cancellationToken = default)
    {
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

    public async Task LoadSavedPathsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        foreach (var path in paths)
        {
            try
            {
                await LoadFromPathAsync(path, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to load path '{path}': {ex.Message}");
            }
        }
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
            var currentRoots = _treeRoot.OfType<RootNode>().ToList();
            var reloadedRoots = new List<RootNode>(currentRoots.Count);

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
                }
            }

            TreeNodeOperations.ReplaceRootSnapshot(_treeRoot, reloadedRoots);
            _stateManager.RestoreState(_treeRoot, state, autoExpandNew: false);
            _lastExecutedNode = TreeNodeOperations.PromoteLatestNewExecutable(_treeRoot, previousExecutableIds) ?? _lastExecutedNode;
            TreeChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    public BaseNode? RemoveNode(BaseNode node)
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

    public void Execute(BaseNode node)
    {
        if (!node.IsExecutable)
            return;

        if (_lastExecutedNode != null && _lastExecutedNode != node)
            _lastExecutedNode.IsLastExecuted = false;

        node.Execute();
        _lastExecutedNode = node;
    }

    public void Dispose()
    {
        _fileWatcher.FileChanged -= OnFileChanged;
        _fileWatcher.Dispose();
        _reloadGate.Dispose();
    }

    #region Private Helpers

    private async Task<ReloadRootResult> ReloadRootAsync(RootNode currentRoot, CancellationToken cancellationToken)
    {
        try
        {
            var provider = _serviceProvider.GetKeyedService<IExecutionProvider>(currentRoot.ProviderType);
            if (provider == null)
            {
                Trace.TraceWarning($"No keyed provider found for execution mode '{currentRoot.ProviderType}'");
                return ReloadRootResult.KeepCurrent();
            }

            var nodes = await provider.DiscoverAsync(currentRoot.RootPath, cancellationToken).ConfigureAwait(true);
            var discoveredRoot = nodes.OfType<RootNode>().FirstOrDefault();
            return discoveredRoot != null
                ? ReloadRootResult.UseDiscovered(discoveredRoot)
                : ReloadRootResult.RemoveCurrent();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to reload path '{currentRoot.RootPath}': {ex.Message}");
            return ReloadRootResult.KeepCurrent();
        }
    }

    private void OnFileChanged(object? sender, FileChangedEventArgs e)
    {
        if (e.ChangeType == FileChangeType.Modified)
            return;

        Utils.DispatcherHelper.RunOnMainThread(async void () =>
        {
            try
            {
                await ReloadAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error reloading after file change: {ex.Message}");
            }
        });
    }

    #endregion

    private readonly record struct ReloadRootResult(RootNode? RootNode, bool KeepExistingRoot)
    {
        public static ReloadRootResult UseDiscovered(RootNode rootNode) => new(rootNode, false);
        public static ReloadRootResult KeepCurrent() => new(null, true);
        public static ReloadRootResult RemoveCurrent() => new(null, false);
    }
}