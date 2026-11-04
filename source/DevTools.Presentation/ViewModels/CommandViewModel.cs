using DevTools.AssemblyIsolation;
using DevTools.Execution.Diagnostics;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.UI.Theme;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using ZLogger;
// ReSharper disable UnusedParameterInPartialMethod

namespace DevTools.Presentation.ViewModels;

public partial class CommandViewModel : ObservableObject, IBusyViewModel
{
    private readonly IExecutionOrchestrator _orchestrator;
    private readonly MemoryViewModel _memoryViewModel;
    private readonly DebugEndpoints? _endpoints;
    private readonly ILogger<CommandViewModel> _logger;
    private readonly DispatcherTimer _searchDebounceTimer;
    private readonly DispatcherTimer? _debugStatusTimer;

    private readonly ObservableCollection<ExecutionNodeBase> _treeRoot = [];

    [ObservableProperty]
    public partial ExecutionNodeBase? SelectedNode { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string BusyMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ExecutionMode? BusyExecutionMode { get; set; }

    [ObservableProperty]
    public partial bool IsDropMaskVisible { get; set; }

    [ObservableProperty]
    public partial bool IsDebuggerConnected { get; set; }

    [ObservableProperty]
    public partial bool IsPythonDebuggerConnected { get; set; }

    [ObservableProperty]
    public partial bool IsIronPythonDebuggerConnected { get; set; }

    [ObservableProperty]
    public partial int PythonDebugPort { get; set; }

    [ObservableProperty]
    public partial int IronPythonDebugPort { get; set; }

    public bool HasDebugger => _endpoints is not null;
    public ObservableCollection<ExecutionNodeBase> FilteredItems { get; } = [];

    public CommandViewModel(
        IExecutionOrchestrator orchestrator,
        MemoryViewModel memoryViewModel,
        ILogger<CommandViewModel> logger,
        DebugEndpoints? endpoints = null)
    {
        _orchestrator = orchestrator;
        _memoryViewModel = memoryViewModel;
        _logger = logger;
        _endpoints = endpoints;
        _orchestrator.TreeChanged += OnTreeChanged;
        _orchestrator.ExecutionProgressChanged += OnExecutionProgressChanged;
        ThemeManager.Current.ActualApplicationThemeChanged += OnThemeChanged;

        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchDebounceTimer.Tick += (_, _) => { _searchDebounceTimer.Stop(); PerformSearch(); };

        if (_endpoints is null) return;

        _debugStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _debugStatusTimer.Tick += (_, _) => UpdateDebuggerStatus();
        _debugStatusTimer.Start();
        UpdateDebuggerStatus();
    }

    private void UpdateDebuggerStatus()
    {
        if (_endpoints is null) return;

        IsPythonDebuggerConnected = _endpoints.CPython.Attached;
        IsIronPythonDebuggerConnected = _endpoints.IronPython.Attached;
        IsDebuggerConnected = IsPythonDebuggerConnected || IsIronPythonDebuggerConnected;
        PythonDebugPort = _endpoints.CPython.Port;
        IronPythonDebugPort = _endpoints.IronPython.Port;
    }

    public async Task LoadSavedPathsAsync()
    {
        await this.WhileBusy("Loading saved paths...", async () =>
        {
            await _orchestrator.LoadSavedPathsAsync();
            UpdateTreeRoot();
        });
    }

    [RelayCommand]
    private async Task LoadAssemblyAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*",
            Title = "Select Assembly DLL"
        };
        if (dialog.ShowDialog() != true) return;
        await this.WhileBusy("Loading assembly...", () => _orchestrator.LoadFromPathAsync(dialog.FileName));
    }

    [RelayCommand]
    private async Task LoadScriptsAsync()
    {
        var selectedFolder = AppUtils.SelectFolder("Select Scripts Folder");
        if (string.IsNullOrEmpty(selectedFolder)) return;
        await this.WhileBusy("Loading scripts...", () => _orchestrator.LoadFromPathAsync(selectedFolder));
    }

    private async Task LoadFromPathAsync(string path)
    {
        if (File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            await this.WhileBusy("Loading assembly...", () => _orchestrator.LoadFromPathAsync(path));
        else if (Directory.Exists(path))
            await this.WhileBusy("Loading scripts...", () => _orchestrator.LoadFromPathAsync(path));
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync(object? parameter)
    {
        var node = parameter as ExecutionNodeBase ?? SelectedNode;
        if (node is not { NodeType: NodeType.Executable }) return;
        await ExecuteNodeAsync(node);
    }

    private bool CanExecute() => !IsBusy && SelectedNode?.NodeType == NodeType.Executable;

    public async void ExecuteLastItem()
    {
        try
        {
            var lastExecuted = FindLastExecutedNode(_treeRoot);
            if (lastExecuted != null) await ExecuteNodeAsync(lastExecuted);
        }
        catch (Exception e)
        {
            _logger.ZLogError(e, $"Execution last item failed: {e.Message}");
        }
    }

    private async Task ExecuteNodeAsync(ExecutionNodeBase node)
    {
        using var memoryScope = _memoryViewModel.BeginOperation((node as ExecutionNode)?.ExecutionMode, node.Name);
        await WhileBusy($"Executing '{node.Name}'...", (node as ExecutionNode)?.ExecutionMode, async () =>
        {
            var result = await _orchestrator.ExecuteAsync(node);
            memoryScope.Complete(success: result.Success);
            if (!result.Success) _logger.ZLogError(result.Exception, $"Execution failed '{node.Name}': {result.Message}");
        });
    }

    private static ExecutionNodeBase? FindLastExecutedNode(IEnumerable<ExecutionNodeBase> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsLastExecuted) return node;
            var child = FindLastExecutedNode(node.Children);
            if (child != null) return child;
        }
        return null;
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        await this.WhileBusy("Reloading nodes...", () => _orchestrator.ReloadAsync());
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        if (SelectedNode == null) return;
        var nextSelection = _orchestrator.RemoveNode(SelectedNode);
        if (nextSelection == null) return;
        SelectedNode = nextSelection;
        nextSelection.IsSelected = true;
    }

    private bool CanRemove() => SelectedNode != null;

    [RelayCommand]
    private void Clear() => _orchestrator.ClearAll();

    [RelayCommand] private void ExpandAll() => ExecutionTreeViewHelper.ExpandAll(_treeRoot);
    [RelayCommand] private void CollapseAll() => ExecutionTreeViewHelper.CollapseAll(_treeRoot);

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

    private string? GetFilePathFromSelectedNode() => SelectedNode switch
    {
        ExecutionNodeRoot root => root.RootPath,
        ExecutionNodeIntermediate intermediate => intermediate.FullPath,
        ExecutionNode execute => execute.SourceFilePath,
        _ => null
    };

    private void OnTreeChanged(object? sender, EventArgs e) => UpdateTreeRoot();

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SearchText)) RefreshFilteredItems();
    }

    private void OnExecutionProgressChanged(object? sender, ExecutionProgressEventArgs e)
    {
        if (!IsBusy || string.IsNullOrWhiteSpace(e.Message)) return;
        BusyMessage = e.Message;
    }

    private void UpdateTreeRoot()
    {
        _treeRoot.Clear();
        foreach (var node in _orchestrator.TreeRoot) _treeRoot.Add(node);
        RefreshFilteredItems();
    }

    private void RefreshFilteredItems()
    {
        FilteredItems.Clear();
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            foreach (var node in _treeRoot)
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
        foreach (var node in _treeRoot)
        {
            if (ExecutionTreeViewHelper.FilterNodeRecursive(node, SearchText, ThemeManager.Current.ActualApplicationTheme == AppTheme.Dark))
                FilteredItems.Add(node);
        }
    }

    partial void OnSelectedNodeChanged(ExecutionNodeBase? value)
    {
        ExecuteCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        OpenLocationCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value) => ExecuteCommand.NotifyCanExecuteChanged();
    partial void OnSearchTextChanged(string value) => RefreshFilteredItems();

    private async Task WhileBusy(string message, ExecutionMode? executionMode, Func<Task> action)
    {
        BusyExecutionMode = executionMode;
        try
        {
            await this.WhileBusy(message, action);
        }
        finally
        {
            BusyExecutionMode = null;
        }
    }

    public void ShowDropMask() => IsDropMaskVisible = true;

    public async Task HandleDropAsync(string[] paths, Func<string, bool> isSupportedPath)
    {
        IsDropMaskVisible = false;
        foreach (var path in paths)
            await ProcessDroppedItemAsync(path, isSupportedPath);
    }

    private async Task ProcessDroppedItemAsync(string path, Func<string, bool> isSupportedPath)
    {
        if (!isSupportedPath(path))
        {
            _logger.ZLogWarning($"Unsupported drop item: {path}. Only .dll files and folders are supported.");
            return;
        }
        if (File.Exists(path))
            await ProcessDroppedDllFileAsync(path);
        else if (Directory.Exists(path))
            await LoadFromPathAsync(path);
    }

    private async Task ProcessDroppedDllFileAsync(string filePath)
    {
        if (ManagedAssembly.IsManaged(filePath))
            await LoadFromPathAsync(filePath);
        else
            _logger.ZLogWarning($"File {filePath} is not a valid managed assembly.");
    }
}
