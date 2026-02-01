using RevitDevTool.AddinManager;
using RevitDevTool.AddinManager.Models;
using RevitDevTool.Settings;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using UIFramework;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
// ReSharper disable UnusedParameterInPartialMethod

namespace RevitDevTool.ViewModel;

/// <summary>
/// ViewModel for the AddinLoad view that handles DLL loading and command execution
/// with hierarchical tree structure (Assembly -> Namespace -> Command)
/// </summary>
public partial class AddinLoadViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ObservableCollection<AssemblyNode> _allAssemblies = [];
    private AppTheme _currentTheme;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AssemblyNode> _filteredAssemblies = [];

    [ObservableProperty]
    private object? _selectedItem;
    
    public AddinItem? LastExecutedItem { get; private set; }

    public AddinLoadViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        FilteredAssemblies = _allAssemblies;
        _currentTheme = ThemeManager.Current.ActualApplicationTheme;
        ThemeManager.Current.ActualApplicationThemeChanged += OnThemeChanged;
        LoadSavedAssemblies();
    }
    
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _currentTheme = ThemeManager.Current.ActualApplicationTheme;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            FilterAssemblies();
        }
    }

    [RelayCommand]
    private void LoadAddin()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Assembly files (*.dll)|*.dll|All files (*.*)|*.*",
            Title = "Select Revit Add-in Assembly",
        };

        if (dialog.ShowDialog(owner: MainWindow.getMainWnd()) == true)
        {
            LoadAssembly(dialog.FileName);
        }
    }

    public void LoadAssembly(string filePath)
    {
        try
        {
            var commands = AddinLoaderService.ParseCommands(filePath);

            if (commands.Count == 0)
            {
                Trace.TraceWarning($"No commands found in {filePath}");
                return;
            }

            // Remove existing assembly if it exists
            var existing = _allAssemblies.FirstOrDefault(a => a.FilePath == filePath);
            if (existing != null) _allAssemblies.Remove(existing);

            // Create new assembly node with namespace grouping
            var assemblyNode = new AssemblyNode(filePath);
            assemblyNode.GroupByNamespace(commands);

            _allAssemblies.Add(assemblyNode);
            FilterAssemblies();
            UpdateAssemblyPathsSetting();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error loading assembly: {ex}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private void Execute(object? parameter = null)
    {
        var itemToExecute = parameter ?? SelectedItem;
        var addinItem = itemToExecute switch
        {
            CommandNode cmd => cmd.AddinItem,
            AddinItem item => item,
            _ => null
        };

        if (addinItem == null) return;

        Trace.TraceInformation($"Execution requested for {addinItem.FullClassName}");
        RunActiveCommand(addinItem);
    }

    private void RunActiveCommand(AddinItem addinItem)
    {
        var filePath = addinItem.AssemblyPath;
        if (!File.Exists(filePath))
        {
            Trace.TraceError("File not found: " + filePath);
            return;
        }

        var message = string.Empty;
        LastExecutedItem = addinItem;
        CommandExecutor.RunCommand(addinItem, AddinLoadHelper.ExternalCommandData, ref message, AddinLoadHelper.ElementSet);
    }

    private bool CanExecute(object? parameter = null)
    {
        var item = parameter ?? SelectedItem;
        return item is CommandNode or AddinItem;
    }

    [RelayCommand]
    private void Remove()
    {
        switch (SelectedItem)
        {
            case null:
                return;
            case TreeNodeBase currentNode:
                currentNode.IsSelected = false;
                if (currentNode is CommandNode cmd 
                    && cmd.AddinItem == LastExecutedItem)
                {
                    LastExecutedItem = null;
                }
                break;
        }

        var nextSelection = SelectedItem switch
        {
            AssemblyNode assembly => HandleRemoveAssembly(assembly),
            NamespaceNode namespaceNode => HandleRemoveNamespace(namespaceNode),
            CommandNode command => HandleRemoveCommand(command),
            _ => null
        };

        FilterAssemblies();
        
        // Update both ViewModel property and TreeNode property
        SelectedItem = nextSelection;
        if (nextSelection is not null)
            nextSelection.IsSelected = true;
    }

    private TreeNodeBase? HandleRemoveAssembly(AssemblyNode assembly)
    {
        _allAssemblies.Remove(assembly);
        UpdateAssemblyPathsSetting();
        return null;
    }

    private TreeNodeBase? HandleRemoveNamespace(NamespaceNode namespaceNode)
    {
        foreach (var assembly in _allAssemblies)
        {
            if (!assembly.Children.Contains(namespaceNode)) continue;

            var index = assembly.Children.IndexOf(namespaceNode);
            assembly.Children.Remove(namespaceNode);
            
            var assemblyRemoved = TryRemoveEmptyAssembly(assembly, out var nextAssembly);
            return assemblyRemoved ? nextAssembly : SelectSiblingOrParent(assembly.Children, index, assembly);
        }
        return null;
    }

    private TreeNodeBase? HandleRemoveCommand(CommandNode command)
    {
        foreach (var assembly in _allAssemblies)
        {
            foreach (var ns in assembly.Children.OfType<NamespaceNode>())
            {
                if (!ns.Children.Contains(command)) continue;

                var index = ns.Children.IndexOf(command);
                ns.Children.Remove(command);
                
                return HandlePostCommandRemoval(assembly, ns, index);
            }
        }
        return null;
    }

    private TreeNodeBase? HandlePostCommandRemoval(AssemblyNode assembly, NamespaceNode ns, int commandIndex)
    {
        // Check if namespace will become empty after this removal
        if (ns.Children.Count == 0)
        {
            var nsIndex = assembly.Children.IndexOf(ns);
            assembly.Children.Remove(ns);
            
            // Try to remove assembly if now empty
            var assemblyRemoved = TryRemoveEmptyAssembly(assembly, out var nextAssembly);
            if (assemblyRemoved)
                return nextAssembly;
            
            // Select a command from next/previous namespace to maintain delete chain
            return SelectCommandFromAdjacentNamespace(assembly, nsIndex);
        }
        
        // Namespace still has commands, select next/previous command
        return SelectSiblingOrParent(ns.Children, commandIndex, ns);
    }
    
    private static TreeNodeBase SelectCommandFromAdjacentNamespace(AssemblyNode assembly, int removedNsIndex)
    {
        // Try next namespace's first command
        if (removedNsIndex < assembly.Children.Count)
        {
            var nextNs = assembly.Children[removedNsIndex] as NamespaceNode;
            if (nextNs?.HasChildren == true)
                return nextNs.Children[0]; // First command of next namespace
        }
        
        // Try previous namespace's last command
        if (removedNsIndex > 0)
        {
            var prevNs = assembly.Children[removedNsIndex - 1] as NamespaceNode;
            if (prevNs?.HasChildren == true)
                return prevNs.Children[^1]; // Last command of previous namespace
        }
        
        // No adjacent namespace with commands, fall back to first available namespace
        var firstNsWithCommands = assembly.Children.OfType<NamespaceNode>().FirstOrDefault(n => n.HasChildren);
        if (firstNsWithCommands != null)
            return firstNsWithCommands.Children[0];
        
        // No commands left, select assembly
        return assembly;
    }

    private bool TryRemoveEmptyAssembly(AssemblyNode assembly, out TreeNodeBase? nextSelection)
    {
        nextSelection = null;
        
        if (assembly.HasChildren)
            return false;
        
        var assemblyIndex = _allAssemblies.IndexOf(assembly);
        _allAssemblies.Remove(assembly);
        UpdateAssemblyPathsSetting();
        
        // Try to select sibling assembly
        if (assemblyIndex > 0)
            nextSelection = _allAssemblies[assemblyIndex - 1];
        else if (_allAssemblies.Count > 0)
            nextSelection = _allAssemblies[0];
        
        return true;
    }

    private static TreeNodeBase SelectSiblingOrParent(
        ObservableCollection<TreeNodeBase> siblings, 
        int removedIndex, 
        TreeNodeBase parent)
    {
        // Try next sibling first (same position after removal)
        // This allows continuous delete presses without reselecting
        if (siblings.Count > removedIndex)
            return siblings[removedIndex];
        
        // Then try previous sibling
        if (removedIndex > 0 && siblings.Count > 0)
            return siblings[removedIndex - 1];
        
        // Fall back to parent
        return parent;
    }

    [RelayCommand]
    private void Clear()
    {
        _allAssemblies.Clear();
        FilterAssemblies();
        UpdateAssemblyPathsSetting();
    }

    [RelayCommand(CanExecute = nameof(CanExpandCollapse))]
    private void ExpandAll()
    {
        SetExpanded(_allAssemblies, true);
    }

    [RelayCommand(CanExecute = nameof(CanExpandCollapse))]
    private void CollapseAll()
    {
        SetExpanded(_allAssemblies, false);
    }

    [RelayCommand(CanExecute = nameof(CanExpandCollapse))]
    private void ToggleAll()
    {
        ToggleExpanded(_allAssemblies);
    }

    private bool CanExpandCollapse()
    {
        return string.IsNullOrWhiteSpace(SearchText);
    }

    private static void SetExpanded(IEnumerable<TreeNodeBase> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = expanded;
            if (node.HasChildren)
            {
                SetExpanded(node.Children, expanded);
            }
        }
    }

    private static void ToggleExpanded(IEnumerable<TreeNodeBase> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = !node.IsExpanded;
            if (node.HasChildren)
            {
                ToggleExpanded(node.Children);
            }
        }
    }

    [RelayCommand]
    private void OpenLocation()
    {
        var filePath = SelectedItem switch
        {
            AssemblyNode assembly => assembly.FilePath,
            CommandNode cmd => cmd.AddinItem.AssemblyPath,
            AddinItem item => item.AssemblyPath,
            _ => null
        };

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

    partial void OnSearchTextChanged(string value)
    {
        FilterAssemblies();
        ExpandAllCommand.NotifyCanExecuteChanged();
        CollapseAllCommand.NotifyCanExecuteChanged();
        ToggleAllCommand.NotifyCanExecuteChanged();
    }

    private void FilterAssemblies()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ClearHighlights(_allAssemblies);
            FilteredAssemblies = _allAssemblies;
            return;
        }

        var lowerSearch = SearchText.ToLower();
        var results = new ObservableCollection<AssemblyNode>();

        foreach (var assembly in _allAssemblies)
        {
            var clonedAssembly = FilterAndHighlight(assembly, lowerSearch);
            if (clonedAssembly != null)
            {
                results.Add(clonedAssembly);
            }
        }

        FilteredAssemblies = results;
    }

    private AssemblyNode? FilterAndHighlight(AssemblyNode assembly, string searchText)
    {
        var assemblyMatches = assembly.DisplayName.ToLower().Contains(searchText);
        var clonedAssembly = new AssemblyNode(assembly.FilePath);

        // Check assembly name match
        if (assemblyMatches)
        {
            return CreateAssemblyMatchResult(assembly, searchText, clonedAssembly);
        }

        // Check namespace and command matches
        ProcessNamespaceAndCommandMatches(assembly, searchText, clonedAssembly);

        return clonedAssembly.HasChildren ? clonedAssembly : null;
    }

    private AssemblyNode CreateAssemblyMatchResult(AssemblyNode assembly, string searchText, AssemblyNode clonedAssembly)
    {
        clonedAssembly.HighlightRange = CreateHighlightRange(assembly.DisplayName, searchText);
        clonedAssembly.IsExpanded = true;
        
        // Include all children and expand namespaces
        foreach (var ns in assembly.Children.OfType<NamespaceNode>())
        {
            var clonedNs = new NamespaceNode(ns.Namespace) { IsExpanded = true };
            foreach (var cmd in ns.Children.OfType<CommandNode>())
            {
                clonedNs.AddCommand(cmd.AddinItem);
            }
            clonedAssembly.Children.Add(clonedNs);
        }
        
        return clonedAssembly;
    }

    private void ProcessNamespaceAndCommandMatches(AssemblyNode assembly, string searchText, AssemblyNode clonedAssembly)
    {
        foreach (var ns in assembly.Children.OfType<NamespaceNode>())
        {
            var nsMatches = ns.Namespace.ToLower().Contains(searchText);
            var clonedNs = new NamespaceNode(ns.Namespace);
            
            if (nsMatches)
            {
                clonedNs.HighlightRange = CreateHighlightRange(ns.Namespace, searchText);
                clonedNs.IsExpanded = true;
            }

            // Check commands
            ProcessCommandMatches(ns, searchText, nsMatches, clonedNs);

            if (!clonedNs.HasChildren) continue;
            clonedAssembly.Children.Add(clonedNs);
            clonedAssembly.IsExpanded = true;
            clonedNs.IsExpanded = true;
        }
    }

    private void ProcessCommandMatches(NamespaceNode ns, string searchText, bool nsMatches, NamespaceNode clonedNs)
    {
        foreach (var cmd in ns.Children.OfType<CommandNode>())
        {
            var cmdMatches = cmd.DisplayName.ToLower().Contains(searchText) ||
                            cmd.AddinItem.FullClassName.ToLower().Contains(searchText);

            if (!nsMatches && !cmdMatches) continue;

            var clonedCmd = new CommandNode(cmd.AddinItem);
            if (cmdMatches)
            {
                clonedCmd.HighlightRange = CreateHighlightRange(cmd.DisplayName, searchText);
            }
            clonedNs.Children.Add(clonedCmd);
        }
    }

    private HighlightRange CreateHighlightRange(string text, string searchText)
    {
        var index = text.ToLower().IndexOf(searchText.ToLower(), StringComparison.Ordinal);
        if (index < 0) return new HighlightRange(-1, -1);
        
        return new HighlightRange(index, index + searchText.Length)
        {
            DarkSkin = _currentTheme == AppTheme.Dark
        };
    }

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
    /// Loads assemblies from saved settings on startup
    /// </summary>
    private void LoadSavedAssemblies()
    {
        var savedPaths = _settingsService.AddinLoadConfig.AssemblyPaths;
        if (savedPaths.Count == 0) return;

        foreach (var path in savedPaths.ToList())  // ToList to avoid modification during iteration
        {
            if (File.Exists(path))
            {
                LoadAssembly(path);
            }
            else
            {
                Trace.TraceWarning($"Assembly file not found for the last session: {path}");
            }
        }
    }

    /// <summary>
    /// Saves current assembly paths to settings
    /// </summary>
    private void UpdateAssemblyPathsSetting()
    {
        var paths = _allAssemblies.Select(a => a.FilePath).ToList();
        _settingsService.AddinLoadConfig.AssemblyPaths.Clear();
        foreach (var path in paths)
        {
            _settingsService.AddinLoadConfig.AssemblyPaths.Add(path);
        }
    }
}
