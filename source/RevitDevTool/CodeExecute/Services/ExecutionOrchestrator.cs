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
        // Auto-detect provider using CanHandle + Priority
        var provider = _serviceProvider.GetServices<IExecutionProvider>().Where(p => p.CanHandle(path)).OrderByDescending(p => p.Priority).FirstOrDefault();

        if (provider == null)
            throw new ArgumentException($"No suitable provider found for path: {path}");

        if (!provider.ValidatePath(path))
            throw new ArgumentException($"Invalid path for provider '{provider.Name}': {path}");

        var state = _stateManager.CaptureState(_treeRoot);
        var discoveredNodes = await provider.DiscoverAsync(path, cancellationToken).ConfigureAwait(true);
        PatchTree(discoveredNodes);

        // Restore state (with auto-expand for new nodes)
        _stateManager.RestoreState(_treeRoot, state, autoExpandNew: true);

        // Setup file watching
        var watchPatterns = provider.GetWatchPatterns();
        _fileWatcher.Watch(path, watchPatterns);

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
        if (_treeRoot.Count == 0)
            return;

        var state = _stateManager.CaptureState(_treeRoot);
        var newExecutables = new List<BaseNode>();

        // Collect all root nodes with their paths and provider types
        var rootInfos = _treeRoot.OfType<RootNode>().Select(n => new { Path = n.RootPath, n.ProviderType }).Distinct().ToList();

        _treeRoot.Clear();

        // Reload each path with appropriate provider
        foreach (var rootInfo in rootInfos)
        {
            try
            {
                var providerKey = rootInfo.ProviderType.ToString();
                var provider = _serviceProvider.GetKeyedService<IExecutionProvider>(providerKey);

                if (provider == null) continue;
                var nodes = await provider.DiscoverAsync(rootInfo.Path, cancellationToken).ConfigureAwait(true);
                PatchTree(nodes, newExecutables);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to reload path '{rootInfo.Path}': {ex.Message}");
            }
        }

        _stateManager.RestoreState(_treeRoot, state, autoExpandNew: false);
        MarkLastNewExecutable(newExecutables);
        TreeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Remove a node from the tree.
    /// Returns the next sibling for selection, or null if none available.
    /// Also removes empty parent containers and unwatches the path if removing a RootNode.
    /// </summary>
    public BaseNode? RemoveNode(BaseNode node)
    {
        // If removing a RootNode, unwatch its path
        if (node is RootNode rootNode)
        {
            _fileWatcher.Unwatch(rootNode.RootPath);
        }

        // Try to remove from root level
        var rootIndex = _treeRoot.IndexOf(node);
        if (rootIndex >= 0)
        {
            // Find next sibling at root level
            var nextSelection = GetNextSibling(_treeRoot, rootIndex);
            _treeRoot.RemoveAt(rootIndex);
            TreeChanged?.Invoke(this, EventArgs.Empty);
            return nextSelection;
        }

        // Search in children
        foreach (var root in _treeRoot.ToList())
        {
            var result = RemoveNodeRecursive(root, node, _treeRoot);
            if (!result.Removed) continue;
            TreeChanged?.Invoke(this, EventArgs.Empty);
            return result.NextSelection;
        }

        return null;
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

        // Clear previous executed node's indicator
        if (_lastExecutedNode != null && _lastExecutedNode != node)
        {
            _lastExecutedNode.IsLastExecuted = false;
        }

        node.Execute();
        _lastExecutedNode = node;
    }

    public void Dispose()
    {
        _fileWatcher.FileChanged -= OnFileChanged;
        _fileWatcher.Dispose();
    }

    #region Private Helpers

    private void PatchTree(IEnumerable<BaseNode> newNodes, List<BaseNode>? newExecutables = null)
    {
        foreach (var newNode in newNodes)
        {
            // Find existing node with same Id
            var existingNode = _treeRoot.FirstOrDefault(n => n.Id == newNode.Id);

            if (existingNode == null)
            {
                _treeRoot.Add(newNode);

                // Track new executables
                if (newExecutables != null)
                {
                    CollectExecutables(newNode, newExecutables);
                }
            }
            else
            {
                // Update existing node (preserve state, update children)
                PatchNodeRecursive(existingNode, newNode, newExecutables);
            }
        }
    }

    private static void CollectExecutables(BaseNode node, List<BaseNode> executables)
    {
        if (node.IsExecutable)
        {
            executables.Add(node);
        }

        foreach (var child in node.Children)
        {
            CollectExecutables(child, executables);
        }
    }

    private void MarkLastNewExecutable(List<BaseNode> newExecutables)
    {
        if (newExecutables.Count == 0) return;

        // Get the last new executable
        var lastNew = newExecutables[^1];

        // Clear previous LastExecuted
        if (_lastExecutedNode != null && _lastExecutedNode != lastNew)
        {
            _lastExecutedNode.IsLastExecuted = false;
        }

        // Mark new one
        lastNew.IsLastExecuted = true;
        _lastExecutedNode = lastNew;
    }

    private static void PatchNodeRecursive(BaseNode existing, BaseNode updated, List<BaseNode>? newExecutables = null)
    {
        // Patch children
        foreach (var updatedChild in updated.Children)
        {
            var existingChild = existing.Children.FirstOrDefault(c => c.Id == updatedChild.Id);

            if (existingChild == null)
            {
                existing.Children.Add(updatedChild);

                // Track new executables
                if (newExecutables != null)
                {
                    CollectExecutables(updatedChild, newExecutables);
                }
            }
            else
            {
                PatchNodeRecursive(existingChild, updatedChild, newExecutables);
            }
        }

        // Remove children that no longer exist
        var childrenToRemove = existing.Children.Where(c => updated.Children.All(uc => uc.Id != c.Id)).ToList();

        foreach (var child in childrenToRemove)
        {
            existing.Children.Remove(child);
        }
    }

    private (bool Removed, BaseNode? NextSelection) RemoveNodeRecursive(BaseNode parent, BaseNode nodeToRemove, IList<BaseNode> parentCollection)
    {
        var childIndex = parent.Children.IndexOf(nodeToRemove);
        return childIndex >= 0 ? RemoveChildAtIndex(parent, childIndex, parentCollection) : RemoveFromChildrenRecursive(parent, nodeToRemove, parentCollection);
    }

    private (bool Removed, BaseNode? NextSelection) RemoveChildAtIndex(BaseNode parent, int childIndex, IList<BaseNode> parentCollection)
    {
        var nextSelection = GetNextSibling(parent.Children, childIndex);
        parent.Children.RemoveAt(childIndex);
        return TryCascadeDeleteEmptyParent(parent, parentCollection, nextSelection);
    }

    private (bool Removed, BaseNode? NextSelection) RemoveFromChildrenRecursive(BaseNode parent, BaseNode nodeToRemove, IList<BaseNode> parentCollection)
    {
        foreach (var child in parent.Children.ToList())
        {
            var result = RemoveNodeRecursive(child, nodeToRemove, parent.Children);
            if (!result.Removed) continue;

            // Check cascade after child removal
            if (parent.Children.Count == 0 && parent.NodeType == NodeType.Container)
            {
                return TryCascadeDeleteEmptyParent(parent, parentCollection, result.NextSelection);
            }
            return result;
        }
        return (false, null);
    }

    private (bool Removed, BaseNode? NextSelection) TryCascadeDeleteEmptyParent(BaseNode parent, IList<BaseNode> parentCollection, BaseNode? currentNextSelection)
    {
        if (parent.Children.Count > 0 || parent.NodeType != NodeType.Container)
        {
            return (true, currentNextSelection);
        }

        var parentIndex = parentCollection.IndexOf(parent);
        if (parentIndex < 0)
        {
            return (true, currentNextSelection);
        }

        // Unwatch if cascade-deleting a RootNode
        if (parent is RootNode rootNode)
        {
            _fileWatcher.Unwatch(rootNode.RootPath);
        }

        var nextSelection = GetNextSibling(parentCollection, parentIndex);
        parentCollection.RemoveAt(parentIndex);
        return (true, nextSelection);
    }

    private static BaseNode? GetNextSibling(IList<BaseNode> siblings, int removedIndex)
    {
        if (siblings.Count <= 1)
            return null;

        // Prefer next sibling, fallback to previous
        return removedIndex < siblings.Count - 1 ? siblings[removedIndex + 1] : siblings[removedIndex - 1];
    }

    private void OnFileChanged(object? sender, FileChangedEventArgs e)
    {
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
}