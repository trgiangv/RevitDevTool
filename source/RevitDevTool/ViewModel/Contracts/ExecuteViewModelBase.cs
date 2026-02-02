using System.Collections.ObjectModel;
using System.Diagnostics;
using RevitDevTool.CodeExecute.Shared.Models;
using RevitDevTool.Settings;
using RevitDevTool.Theme;
// ReSharper disable UnusedParameterInPartialMethod
namespace RevitDevTool.ViewModel.Contracts;

/// <summary>
/// Abstract base class for code execution ViewModels
/// Contains shared functionality for tree management, search, and common operations
/// </summary>
public abstract partial class ExecuteViewModelBase : ObservableObject
{
    protected readonly ISettingsService SettingsService;
    private AppTheme CurrentTheme { get; set; }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private TreeNodeBase? _selectedItem;

    protected ExecuteViewModelBase(ISettingsService settingsService)
    {
        SettingsService = settingsService;
        CurrentTheme = ThemeManager.Current.ActualApplicationTheme;
        ThemeManager.Current.ActualApplicationThemeChanged += OnThemeChanged;
        
        ExpandAllCommand = new RelayCommand(ExpandAll);
        CollapseAllCommand = new RelayCommand(CollapseAll);
        ToggleAllCommand = new RelayCommand(ToggleAll);
        ClearCommand = new RelayCommand(Clear);
        OpenLocationCommand = new RelayCommand(OpenLocation, CanOpenLocation);
    }

    private bool CanOpenLocation()
    {
        return !string.IsNullOrEmpty(GetFilePathFromSelectedItem());
    }
    
    partial void OnSearchTextChanged(string value)
    {
        RefreshFilteredItems();
    }

    partial void OnSelectedItemChanged(TreeNodeBase? value)
    {
        ExecuteCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        OpenLocationCommand.NotifyCanExecuteChanged();
        RenameGroupCommand?.NotifyCanExecuteChanged();
        AddNewScriptCommand?.NotifyCanExecuteChanged();
        SelectScriptCommand?.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Filtered tree items - concrete implementation with backing field for proper WPF binding
    /// </summary>
    public ObservableCollection<TreeNodeBase> FilteredItems { get; } = [];

    /// <summary>
    /// Display text for the load button - implemented by derived classes
    /// </summary>
    public abstract string LoadButtonText { get; }

    public abstract IRelayCommand LoadCommand { get; }
    public abstract IRelayCommand ExecuteCommand { get; }
    public abstract IRelayCommand RemoveCommand { get; }

    // Optional commands for Python mode
    public virtual IRelayCommand? RenameGroupCommand => null;
    public virtual IRelayCommand? AddNewScriptCommand => null;
    public virtual IRelayCommand? SelectScriptCommand => null;

    /// <summary>
    /// Execute the last executed item (if any)
    /// </summary>
    public abstract void ExecuteLastItem();

    /// <summary>
    /// Expand all tree nodes
    /// </summary>
    public IRelayCommand ExpandAllCommand { get; }

    /// <summary>
    /// Collapse all tree nodes
    /// </summary>
    public IRelayCommand CollapseAllCommand { get; }

    /// <summary>
    /// Toggle expand/collapse all nodes
    /// </summary>
    public IRelayCommand ToggleAllCommand { get; }

    /// <summary>
    /// Clear all items - implemented by derived classes
    /// </summary>
    public IRelayCommand ClearCommand { get; }

    /// <summary>
    /// Open file location in Windows Explorer
    /// </summary>
    public IRelayCommand OpenLocationCommand { get; }

    private void ExpandAll()
    {
        foreach (var item in FilteredItems)
        {
            ExpandNode(item);
        }
    }

    private void CollapseAll()
    {
        foreach (var item in FilteredItems)
        {
            CollapseNode(item);
        }
    }

    private void ToggleAll()
    {
        var firstItem = FilteredItems.FirstOrDefault();
        if (firstItem == null) return;

        var shouldExpand = !firstItem.IsExpanded;
        foreach (var item in FilteredItems)
        {
            if (shouldExpand)
                ExpandNode(item);
            else
                CollapseNode(item);
        }
    }

    protected abstract void Clear();

    private void OpenLocation()
    {
        var filePath = GetFilePathFromSelectedItem();
        if (string.IsNullOrEmpty(filePath)) return;

        try
        {
            Process.Start("explorer.exe", $"/select, \"{filePath}\"");
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to open location: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all unfiltered items - implemented by derived classes
    /// </summary>
    protected abstract IEnumerable<TreeNodeBase> GetAllItems();

    /// <summary>
    /// Filter items based on search text - implemented by derived classes
    /// Returns only items that match the search criteria
    /// </summary>
    protected abstract IEnumerable<TreeNodeBase> FilterItemsCore(string searchText);

    /// <summary>
    /// Refresh the FilteredItems collection after data changes.
    /// Call this method after adding, removing, or modifying items.
    /// </summary>
    protected void RefreshFilteredItems()
    {
        FilteredItems.Clear();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ClearHighlights(GetAllItems());
            foreach (var item in GetAllItems())
            {
                FilteredItems.Add(item);
            }
        }
        else
        {
            foreach (var item in FilterItemsCore(SearchText))
            {
                FilteredItems.Add(item);
            }
        }
    }

    /// <summary>
    /// Clear all highlights from nodes recursively
    /// </summary>
    private static void ClearHighlights(IEnumerable<TreeNodeBase> nodes)
    {
        foreach (var node in nodes)
        {
            node.HighlightRange = null;
            if (node.HasChildren)
            {
                ClearHighlights(node.Children);
            }
        }
    }

    /// <summary>
    /// Get file path from selected item - implemented by derived classes
    /// </summary>
    protected abstract string? GetFilePathFromSelectedItem();

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        CurrentTheme = ThemeManager.Current.ActualApplicationTheme;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            RefreshFilteredItems();
        }
    }

    /// <summary>
    /// Apply search highlight to a node based on search text
    /// </summary>
    protected void ApplySearchHighlight(TreeNodeBase node, string searchLower)
    {
        var displayNameLower = node.DisplayName.ToLowerInvariant();
        var startIndex = displayNameLower.IndexOf(searchLower, StringComparison.Ordinal);
        
        if (startIndex >= 0)
        {
            node.HighlightRange = new HighlightRange(startIndex, startIndex + searchLower.Length)
            {
                DarkSkin = CurrentTheme == AppTheme.Dark
            };
        }
    }

    private static void ExpandNode(TreeNodeBase node)
    {
        node.IsExpanded = true;
        foreach (var child in node.Children)
        {
            ExpandNode(child);
        }
    }

    private static void CollapseNode(TreeNodeBase node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
        {
            CollapseNode(child);
        }
    }
}
