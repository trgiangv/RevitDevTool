using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using RevitDevTool.Settings;
using DevTools.UI.Theme;
using System.Windows.Threading;
using RevitDevTool.Execution.Interfaces;
using RevitDevTool.Execution.Models;
using RevitDevTool.Execution.Providers.Python;
using DevTools.McpParser.Models;
using RevitDevTool.Utils;
// ReSharper disable UnusedParameterInPartialMethod
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.ViewModel;

public partial class CommandViewModel : ObservableObject
{
    private readonly IExecutionOrchestrator _orchestrator;
    private readonly ISettingsService _settingsService;
    private readonly MemoryViewModel _memoryViewModel;
    private readonly DispatcherTimer _searchDebounceTimer;
    private readonly DispatcherTimer _debugStatusTimer;
    private int _busyDepth;

    [ObservableProperty]
    private ObservableCollection<ExecutionNodeBase> _treeRoot = [];

    [ObservableProperty]
    private ExecutionNodeBase? _selectedNode;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyMessage = string.Empty;

    [ObservableProperty]
    private ExecutionMode? _busyProviderType;

    public static int DebugPort => PythonDebugger.DebugPort;

    [ObservableProperty]
    private bool _isDebuggerConnected;

    public ObservableCollection<ExecutionNodeBase> FilteredItems { get; } = [];

    public CommandViewModel(
        IExecutionOrchestrator orchestrator,
        ISettingsService settingsService,
        MemoryViewModel memoryViewModel)
    {
        _orchestrator = orchestrator;
        _settingsService = settingsService;
        _memoryViewModel = memoryViewModel;
        _orchestrator.TreeChanged += OnTreeChanged;
        _orchestrator.RootRemoved += OnRootRemoved;
        _orchestrator.ExecutionProgressChanged += OnExecutionProgressChanged;
        ThemeManager.Current.ActualApplicationThemeChanged += OnThemeChanged;

        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            PerformSearch();
        };

        _debugStatusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _debugStatusTimer.Tick += (_, _) => UpdateDebuggerStatus();
        _debugStatusTimer.Start();
        UpdateDebuggerStatus();
    }

    private void UpdateDebuggerStatus()
    {
        IsDebuggerConnected = PythonDebugger.IsConnected();
    }

    public async Task LoadSavedPathsAsync()
    {
        var config = _settingsService.ExecutionConfig;
        var allPaths = config.DotnetAssemblyPaths.Concat(config.ScriptFolderPaths);

        using var _ = BeginBusy("Loading saved paths...");
        await _orchestrator.LoadSavedPathsAsync(allPaths);
        UpdateTreeRoot();
    }

    [RelayCommand]
    private Task LoadAssemblyAsync()
    {
        return LoadAssemblyFromPathAsync(null);
    }

    [RelayCommand]
    private Task LoadScriptsAsync()
    {
        return LoadScriptsFromPathAsync(null);
    }

    private async Task LoadAssemblyFromPathAsync(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*",
                Title = "Select Assembly DLL"
            };

            if (dialog.ShowDialog() == true)
            {
                path = dialog.FileName;
            }
        }

        if (string.IsNullOrEmpty(path))
            return;

        using var _ = BeginBusy("Loading assembly...");
        await _orchestrator.LoadFromPathAsync(path!);

        SavePathToSettings(path!, ExecutionMode.Assembly);
        UpdateTreeRoot();
    }

    private async Task LoadScriptsFromPathAsync(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            var selectedFolder = SettingsUtils.SelectFolder("Select Scripts Folder");
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                path = selectedFolder;
            }
        }

        if (string.IsNullOrEmpty(path)) return;

        using var _ = BeginBusy("Loading scripts...");
        await _orchestrator.LoadFromPathAsync(path!);

        SavePathToSettings(path!, ExecutionMode.Script);
        UpdateTreeRoot();
    }

    public async Task LoadFromPathAsync(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        if (File.Exists(path) && path!.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            await LoadAssemblyFromPathAsync(path);
        }
        else if (Directory.Exists(path))
        {
            await LoadScriptsFromPathAsync(path);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task Execute(object? parameter)
    {
        var node = parameter as ExecutionNodeBase ?? SelectedNode;
        if (node is not { NodeType: NodeType.Executable }) return;
        var provider = (node as ExecutionNode)?.ProviderType.ToString() ?? "Unknown";
        using var memoryScope = _memoryViewModel.BeginOperation(provider, node.Name);
        using var _ = BeginBusy($"Executing '{node.Name}'...", (node as ExecutionNode)?.ProviderType);
        var result = await _orchestrator.ExecuteAsync(node);
        memoryScope.Complete(success: result.Success);
        if (!result.Success)
        {
            Trace.TraceWarning($"Execution failed: {result.Message}");
        }
    }

    private bool CanExecute() => !IsBusy && SelectedNode?.NodeType == NodeType.Executable;

    public void ExecuteLastItem()
    {
        _ = ExecuteLastItemAsync();
    }

    private async Task ExecuteLastItemAsync()
    {
        var lastExecuted = FindLastExecutedNode(TreeRoot);
        if (lastExecuted != null)
        {
            try
            {
                var provider = (lastExecuted as ExecutionNode)?.ProviderType.ToString() ?? "Unknown";
                using var memoryScope = _memoryViewModel.BeginOperation(provider, lastExecuted.Name);
                using var _ = BeginBusy($"Executing '{lastExecuted.Name}'...", (lastExecuted as ExecutionNode)?.ProviderType);
                var result = await _orchestrator.ExecuteAsync(lastExecuted);
                memoryScope.Complete(success: result.Success);
                if (!result.Success)
                {
                    Trace.TraceWarning($"Execution failed: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to execute last item: {ex.Message}");
            }
        }
    }

    private static ExecutionNodeBase? FindLastExecutedNode(IEnumerable<ExecutionNodeBase> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsLastExecuted)
                return node;

            var childResult = FindLastExecutedNode(node.Children);
            if (childResult != null)
                return childResult;
        }
        return null;
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        using var _ = BeginBusy("Reloading nodes...");
        await _orchestrator.ReloadAsync();
        UpdateTreeRoot();
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        switch (SelectedNode)
        {
            case null:
                return;
            case ExecutionNodeRoot rootNode:
                RemovePathFromSettings(rootNode.RootPath);
                break;
        }

        var nextSelection = _orchestrator.RemoveNode(SelectedNode);

        UpdateTreeRoot();

        if (nextSelection == null) return;
        SelectedNode = nextSelection;
        nextSelection.IsSelected = true;
    }

    private bool CanRemove() => SelectedNode != null;

    [RelayCommand]
    private void Clear()
    {
        _orchestrator.ClearAll();
        ClearAllPathsFromSettings();
        UpdateTreeRoot();
    }

    [RelayCommand]
    private void ExpandAll()
    {
        ExecutionTreeViewHelper.ExpandAll(TreeRoot);
    }

    [RelayCommand]
    private void CollapseAll()
    {
        ExecutionTreeViewHelper.CollapseAll(TreeRoot);
    }

    [RelayCommand]
    private void ToggleAll()
    {
        if (FilteredItems.Count == 0) return;
        ExecutionTreeViewHelper.ToggleAll(FilteredItems);
    }

    [RelayCommand(CanExecute = nameof(CanOpenLocation))]
    private void OpenLocation()
    {
        var filePath = GetFilePathFromSelectedNode();
        if (string.IsNullOrEmpty(filePath)) return;
        Process.Start("explorer.exe", $"/select, \"{filePath}\"");
    }

    private bool CanOpenLocation() => !string.IsNullOrEmpty(GetFilePathFromSelectedNode());

    private string? GetFilePathFromSelectedNode()
    {
        if (SelectedNode == null) return null;

        return SelectedNode switch
        {
            ExecutionNodeRoot root => root.RootPath,
            ExecutionNodeIntermediate intermediate => intermediate.FullPath,
            ExecutionNode execute => execute.SourceFilePath,
            _ => null
        };
    }

    private void OnTreeChanged(object? sender, EventArgs e)
    {
        UpdateTreeRoot();
    }

    private void OnRootRemoved(object? sender, RootRemovedEventArgs e)
    {
        RemovePathFromSettings(e.RootPath);

        if (e.IsRename)
        {
            SavePathToSettings(e.NewPath!, ExecutionMode.Script);
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            RefreshFilteredItems();
        }
    }

    private void OnExecutionProgressChanged(object? sender, ExecutionProgressEventArgs e)
    {
        if (!IsBusy) return;
        if (string.IsNullOrWhiteSpace(e.Message)) return;
        BusyMessage = e.Message;
    }

    private void UpdateTreeRoot()
    {
        TreeRoot.Clear();
        foreach (var node in _orchestrator.TreeRoot)
        {
            TreeRoot.Add(node);
        }

        RefreshFilteredItems();
    }

    private void RefreshFilteredItems()
    {
        FilteredItems.Clear();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            foreach (var node in TreeRoot)
            {
                ExecutionTreeViewHelper.SetVisibilityRecursive(node, true);
                ExecutionTreeViewHelper.ClearHighlightsRecursive(node);
                FilteredItems.Add(node);
            }
        }
        else
        {
            PerformSearch();
        }
    }

    private void PerformSearch()
    {
        FilteredItems.Clear();

        foreach (var node in TreeRoot)
        {
            if (ExecutionTreeViewHelper.FilterNodeRecursive(
                    node,
                    SearchText,
                    ThemeManager.Current.ActualApplicationTheme == AppTheme.Dark))
            {
                FilteredItems.Add(node);
            }
        }
    }

    partial void OnSelectedNodeChanged(ExecutionNodeBase? value)
    {
        ExecuteCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        OpenLocationCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        ExecuteCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilteredItems();
    }

    private void SavePathToSettings(string path, ExecutionMode mode)
    {
        var config = _settingsService.ExecutionConfig;

        var list = mode switch
        {
            ExecutionMode.Assembly => config.DotnetAssemblyPaths,
            ExecutionMode.Script => config.ScriptFolderPaths,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

        if (!list.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(path);
        }
    }

    private void RemovePathFromSettings(string path)
    {
        var config = _settingsService.ExecutionConfig;
        config.DotnetAssemblyPaths.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
        config.ScriptFolderPaths.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    private void ClearAllPathsFromSettings()
    {
        var config = _settingsService.ExecutionConfig;
        config.DotnetAssemblyPaths.Clear();
        config.ScriptFolderPaths.Clear();
    }

    private BusyScope BeginBusy(string message, ExecutionMode? providerType = null)
    {
        _busyDepth++;
        IsBusy = true;
        BusyMessage = message;
        BusyProviderType = providerType;
        return new BusyScope(this);
    }

    private void EndBusy()
    {
        _busyDepth = Math.Max(0, _busyDepth - 1);
        if (_busyDepth != 0) return;

        IsBusy = false;
        BusyMessage = string.Empty;
        BusyProviderType = null;
    }

    private sealed class BusyScope(CommandViewModel owner) : IDisposable
    {
        private CommandViewModel? _owner = owner;

        public void Dispose()
        {
            _owner?.EndBusy();
            _owner = null;
        }
    }
}
