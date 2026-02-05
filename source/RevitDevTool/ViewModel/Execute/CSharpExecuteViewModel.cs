using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using RevitDevTool.CodeExecute.CSharp;
using RevitDevTool.CodeExecute.CSharp.Models;
using RevitDevTool.CodeExecute.Shared.Models;
using RevitDevTool.Controllers;
using RevitDevTool.Settings;
using RevitDevTool.Utils;
using UIFramework;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace RevitDevTool.ViewModel.Execute;

/// <summary>
/// ViewModel for CSharp (.dll) code execution
/// </summary>
public partial class CSharpExecuteViewModel : Contracts.ExecuteViewModelBase
{
    private readonly ObservableCollection<AssemblyNode> _allAssemblies = [];

    public override string LoadButtonText => "Load Add-in...";

    private AddinItem? LastExecutedItem { get; set; }

    public override IRelayCommand LoadCommand => LoadAddinCommand;
    public override IRelayCommand ExecuteCommand => DoExecuteCommand;
    public override IRelayCommand RemoveCommand => DoRemoveCommand;

    public override void ExecuteLastItem()
    {
        if (LastExecutedItem is null)
        {
            Trace.TraceWarning("No last executed C# add-in found.");
            return;
        }
        RunActiveCommand(LastExecutedItem);
    }

    public CSharpExecuteViewModel(ISettingsService settingsService) : base(settingsService)
    {
        LoadSavedAssemblies();
        RefreshFilteredItems();
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

            var existing = _allAssemblies.FirstOrDefault(a => a.FilePath == filePath);
            if (existing != null) _allAssemblies.Remove(existing);

            var assemblyNode = new AssemblyNode(filePath);
            assemblyNode.GroupByNamespace(commands);

            _allAssemblies.Add(assemblyNode);
            RefreshFilteredItems();
            UpdateAssemblyPathsSetting();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error loading assembly: {ex}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private void DoExecute(object? parameter = null)
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
        ExternalEventController.ActionEventHandler.Raise(_ =>
        {
            CommandExecutor.RunCommand(addinItem, AddinLoadHelper.ExternalCommandData, ref message, AddinLoadHelper.ElementSet);
        });
    }

    private bool CanExecute(object? parameter = null)
    {
        var item = parameter ?? SelectedItem;
        return item is CommandNode or AddinItem;
    }

    [RelayCommand]
    private void DoRemove()
    {
        switch (SelectedItem)
        {
            case null:
                return;
            case var currentNode:
                currentNode.IsSelected = false;
                if (currentNode is CommandNode cmd && cmd.AddinItem == LastExecutedItem)
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

        RefreshFilteredItems();

        SelectedItem = nextSelection;
        if (nextSelection is not null)
            nextSelection.IsSelected = true;
    }

    [RelayCommand]
    protected override void Clear()
    {
        _allAssemblies.Clear();
        RefreshFilteredItems();
        UpdateAssemblyPathsSetting();
    }

    protected override IEnumerable<TreeNodeBase> GetAllItems()
    {
        return _allAssemblies;
    }

    protected override IEnumerable<TreeNodeBase> FilterItemsCore(string searchText)
    {
        var filtered = new List<TreeNodeBase>();
        var searchLower = searchText.ToLowerInvariant();

        foreach (var assembly in _allAssemblies)
        {
            var filteredAssembly = FilterAssembly(assembly, searchLower);
            if (filteredAssembly != null)
            {
                filtered.Add(filteredAssembly);
            }
        }

        return filtered;
    }

    protected override string? GetFilePathFromSelectedItem()
    {
        return SelectedItem switch
        {
            AssemblyNode assembly => assembly.FilePath,
            CommandNode cmd => cmd.AddinItem.AssemblyPath,
            _ => null
        };
    }

    private AssemblyNode? FilterAssembly(AssemblyNode assembly, string searchLower)
    {
        return assembly.DisplayName.ToLowerInvariant().Contains(searchLower) 
            ? CreateAssemblyWithAllChildren(assembly, searchLower) 
            : CreateAssemblyWithFilteredNamespaces(assembly, searchLower);
    }

    private AssemblyNode CreateAssemblyWithAllChildren(AssemblyNode assembly, string searchLower)
    {
        var result = new AssemblyNode(assembly.FilePath) { IsExpanded = true };
        ApplySearchHighlight(result, searchLower);
        
        foreach (var ns in assembly.Children.OfType<NamespaceNode>())
        {
            var clonedNs = new NamespaceNode(ns.Namespace) { IsExpanded = true };
            foreach (var cmd in ns.Children.OfType<CommandNode>())
            {
                clonedNs.Children.Add(new CommandNode(cmd.AddinItem));
            }
            result.Children.Add(clonedNs);
        }

        return result;
    }

    private AssemblyNode? CreateAssemblyWithFilteredNamespaces(AssemblyNode assembly, string searchLower)
    {
        var filteredNamespaces = new List<NamespaceNode>();
        foreach (var ns in assembly.Children.OfType<NamespaceNode>())
        {
            var filteredNs = FilterNamespace(ns, searchLower);
            if (filteredNs != null)
            {
                filteredNamespaces.Add(filteredNs);
            }
        }

        if (filteredNamespaces.Count == 0)
            return null;

        var filteredResult = new AssemblyNode(assembly.FilePath) { IsExpanded = true };
        foreach (var ns in filteredNamespaces)
        {
            filteredResult.Children.Add(ns);
        }

        return filteredResult;
    }

    private NamespaceNode? FilterNamespace(NamespaceNode ns, string searchLower)
    {
        return ns.DisplayName.ToLowerInvariant().Contains(searchLower) 
            ? CreateNamespaceWithAllCommands(ns, searchLower) 
            : CreateNamespaceWithFilteredCommands(ns, searchLower);
    }

    private NamespaceNode CreateNamespaceWithAllCommands(NamespaceNode ns, string searchLower)
    {
        var result = new NamespaceNode(ns.Namespace) { IsExpanded = true };
        ApplySearchHighlight(result, searchLower);

        foreach (var cmd in ns.Children.OfType<CommandNode>())
        {
            result.Children.Add(new CommandNode(cmd.AddinItem));
        }
        return result;
    }

    private NamespaceNode? CreateNamespaceWithFilteredCommands(NamespaceNode ns, string searchLower)
    {
        var filteredCommands = new List<CommandNode>();
        foreach (var cmd in ns.Children.OfType<CommandNode>())
        {
            if (!cmd.DisplayName.ToLowerInvariant().Contains(searchLower)) continue;
            
            var filtered = new CommandNode(cmd.AddinItem);
            ApplySearchHighlight(filtered, searchLower);
            filteredCommands.Add(filtered);
        }

        if (filteredCommands.Count == 0)
            return null;

        var filteredResult = new NamespaceNode(ns.Namespace) { IsExpanded = true };
        foreach (var cmd in filteredCommands)
        {
            filteredResult.Children.Add(cmd);
        }

        return filteredResult;
    }

    private void LoadSavedAssemblies()
    {
        var config = SettingsService.CodeExecuteConfig;
        foreach (var assemblyPath in config.CSharpAssemblyPaths)
        {
            if (File.Exists(assemblyPath))
            {
                LoadAssembly(assemblyPath);
            }
        }
    }

    private void UpdateAssemblyPathsSetting()
    {
        var config = SettingsService.CodeExecuteConfig;
        config.CSharpAssemblyPaths = _allAssemblies.Select(a => a.FilePath).ToList();
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

    private TreeNodeBase? HandlePostCommandRemoval(AssemblyNode assembly, NamespaceNode ns, int removedIndex)
    {
        var nextNode = SelectSiblingOrParent(ns.Children, removedIndex, ns);
        if (ns.Children.Count != 0) return nextNode;
        assembly.Children.Remove(ns);
        var assemblyRemoved = TryRemoveEmptyAssembly(assembly, out var nextAssembly);
        return assemblyRemoved ? nextAssembly : nextNode;
    }

    private bool TryRemoveEmptyAssembly(AssemblyNode assembly, out TreeNodeBase? nextAssembly)
    {
        if (assembly.Children.Count != 0)
        {
            nextAssembly = null;
            return false;
        }

        var index = _allAssemblies.IndexOf(assembly);
        _allAssemblies.Remove(assembly);
        UpdateAssemblyPathsSetting();

        nextAssembly = index < _allAssemblies.Count ? _allAssemblies[index]
            : index > 0 ? _allAssemblies[index - 1]
            : null;

        return true;
    }

    private static TreeNodeBase SelectSiblingOrParent(ObservableCollection<TreeNodeBase> siblings, int removedIndex, TreeNodeBase parent)
    {
        if (siblings.Count == 0) return parent;
        var newIndex = Math.Min(removedIndex, siblings.Count - 1);
        return siblings[newIndex];
    }
}
