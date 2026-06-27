using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using ZLogger;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Settings;
using DevTools.Presentation.Interfaces;
using DevTools.UI.Theme;
// ReSharper disable UnusedParameterInPartialMethod
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Presentation.ViewModels;

public partial class CommandViewModel : ObservableObject, IBusyViewModel
{
    private readonly IExecutionOrchestrator _orchestrator;
    private readonly ISettingsService _settingsService;
    private readonly MemoryViewModel _memoryViewModel;
    private readonly IDebuggerBridge? _debugger;
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
    public partial bool IsDebuggerConnected { get; set; }

    public bool HasDebugger => _debugger != null;
    public int DebugPort => _debugger?.DebugPort ?? 0;
    public ObservableCollection<ExecutionNodeBase> FilteredItems { get; } = [];

    public CommandViewModel(
        IExecutionOrchestrator orchestrator,
        ISettingsService settingsService,
        MemoryViewModel memoryViewModel,
        ILogger<CommandViewModel> logger,
        IDebuggerBridge? debugger = null)
    {
        _orchestrator = orchestrator;
        _settingsService = settingsService;
        _memoryViewModel = memoryViewModel;
        _logger = logger;
        _debugger = debugger;
        _orchestrator.TreeChanged += OnTreeChanged;
        _orchestrator.RootRemoved += OnRootRemoved;
        _orchestrator.ExecutionProgressChanged += OnExecutionProgressChanged;
        ThemeManager.Current.ActualApplicationThemeChanged += OnThemeChanged;

        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchDebounceTimer.Tick += (_, _) => { _searchDebounceTimer.Stop(); PerformSearch(); };

        if (_debugger != null)
        {
            _debugStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _debugStatusTimer.Tick += (_, _) => UpdateDebuggerStatus();
            _debugStatusTimer.Start();
            UpdateDebuggerStatus();
        }
    }

    private void UpdateDebuggerStatus()
    {
        IsDebuggerConnected = _debugger?.IsConnected() ?? false;
    }

    public async Task LoadSavedPathsAsync()
    {
        var config = _settingsService.ExecutionConfig;
        var allPaths = config.DotnetAssemblyPaths.Concat(config.ScriptFolderPaths).ToList();
        await this.WhileBusy("Loading saved paths...", async () =>
        {
            var failedPaths = await _orchestrator.LoadSavedPathsAsync(allPaths);
            foreach (var path in failedPaths) RemovePathFromSettings(path);
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
        await this.WhileBusy("Loading assembly...", async () =>
        {
            await _orchestrator.LoadFromPathAsync(dialog.FileName);
            SavePathToSettings(dialog.FileName, ContainerMode.Assembly);
            UpdateTreeRoot();
        });
    }

    [RelayCommand]
    private async Task LoadScriptsAsync()
    {
        var selectedFolder = Utilities.AppUtils.SelectFolder("Select Scripts Folder");
        if (string.IsNullOrEmpty(selectedFolder)) return;
        await this.WhileBusy("Loading scripts...", async () =>
        {
            await _orchestrator.LoadFromPathAsync(selectedFolder);
            SavePathToSettings(selectedFolder, ContainerMode.Script);
            UpdateTreeRoot();
        });
    }

    public async Task LoadFromPathAsync(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (File.Exists(path) && path!.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            await this.WhileBusy("Loading assembly...", async () =>
            {
                await _orchestrator.LoadFromPathAsync(path);
                SavePathToSettings(path, ContainerMode.Assembly);
            });
        }
        else if (Directory.Exists(path))
        {
            await this.WhileBusy("Loading scripts...", async () =>
            {
                await _orchestrator.LoadFromPathAsync(path!);
                SavePathToSettings(path!, ContainerMode.Script);
            });
        }
        UpdateTreeRoot();
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task Execute(object? parameter)
    {
        var node = parameter as ExecutionNodeBase ?? SelectedNode;
        if (node is not { NodeType: NodeType.Executable }) return;
        using var memoryScope = _memoryViewModel.BeginOperation((node as ExecutionNode)?.ExecutionMode, node.Name);
        await WhileBusy($"Executing '{node.Name}'...", (node as ExecutionNode)?.ExecutionMode, async () =>
        {
            var result = await _orchestrator.ExecuteAsync(node);
            memoryScope.Complete(success: result.Success);
            if (!result.Success) _logger.ZLogWarning($"Execution failed: {result.Message}");
        });
    }

    private bool CanExecute() => !IsBusy && SelectedNode?.NodeType == NodeType.Executable;

    public void ExecuteLastItem() => _ = ExecuteLastItemAsync();

    private async Task ExecuteLastItemAsync()
    {
        var lastExecuted = FindLastExecutedNode(_treeRoot);
        if (lastExecuted == null) return;
        try
        {
            using var memoryScope = _memoryViewModel.BeginOperation((lastExecuted as ExecutionNode)?.ExecutionMode, lastExecuted.Name);
            await WhileBusy($"Executing '{lastExecuted.Name}'...", (lastExecuted as ExecutionNode)?.ExecutionMode, async () =>
            {
                var result = await _orchestrator.ExecuteAsync(lastExecuted);
                memoryScope.Complete(success: result.Success);
                if (!result.Success) _logger.ZLogWarning($"Execution failed: {result.Message}");
            });
        }
        catch (Exception ex) { _logger.ZLogError($"Failed to execute last item: {ex.Message}"); }
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
        await this.WhileBusy("Reloading nodes...", async () =>
        {
            await _orchestrator.ReloadAsync();
            UpdateTreeRoot();
        });
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        if (SelectedNode == null) return;
        if (SelectedNode is ExecutionNodeRoot rootNode) RemovePathFromSettings(rootNode.RootPath);
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

    private void OnRootRemoved(object? sender, RootRemovedEventArgs e)
    {
        RemovePathFromSettings(e.RootPath);
        if (e.IsRename) SavePathToSettings(e.NewPath!, ContainerMode.Script);
    }

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

    private void SavePathToSettings(string path, ContainerMode container)
    {
        var config = _settingsService.ExecutionConfig;
        var list = container switch
        {
            ContainerMode.Assembly => config.DotnetAssemblyPaths,
            ContainerMode.Script => config.ScriptFolderPaths,
            _ => throw new ArgumentOutOfRangeException(nameof(container))
        };
        if (!list.Contains(path, StringComparer.OrdinalIgnoreCase)) list.Add(path);
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
}
