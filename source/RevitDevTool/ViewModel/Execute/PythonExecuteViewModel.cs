using System.Collections.ObjectModel;
using System.Diagnostics;
using RevitDevTool.CodeExecute.Python;
using RevitDevTool.CodeExecute.Python.Models;
using RevitDevTool.CodeExecute.Shared.Models;
using RevitDevTool.Settings;
using RevitDevTool.Settings.Config;
using PyGroupNode = RevitDevTool.CodeExecute.Python.Models.GroupNode;
using PyScriptNode = RevitDevTool.CodeExecute.Python.Models.ScriptNode;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace RevitDevTool.ViewModel.Execute;

/// <summary>
/// ViewModel for Python (.py) script execution
/// </summary>
public partial class PythonExecuteViewModel : Contracts.ExecuteViewModelBase
{
    public readonly ObservableCollection<PyGroupNode> AllGroups = [];

    public override string LoadButtonText => "Create Group";

    private PyScriptNode? LastExecutedItem { get; set; }

    public override IRelayCommand LoadCommand => CreateGroupCommand;
    public override IRelayCommand ExecuteCommand => ExecuteScriptCommand;
    public override IRelayCommand RemoveCommand => RemoveItemCommand;

    public override IRelayCommand RenameGroupCommand => DoRenameGroupCommand;
    public override IRelayCommand AddNewScriptCommand => DoAddNewScriptCommand;
    public override IRelayCommand SelectScriptCommand => DoSelectScriptCommand;

    public override void ExecuteLastItem()
    {
        if (LastExecutedItem is null)
        {
            Trace.TraceWarning("No last executed Python script found.");
            return;
        }

        var dynFilePath = LastExecutedItem.DynamoScriptItem.Create();
        
        if (string.IsNullOrEmpty(dynFilePath))
        {
            Trace.TraceError($"Failed to create Dynamo file for script: {LastExecutedItem.FilePath}");
            return;
        }
        
        DynamoEngine.RunDynamoGraph(dynFilePath);
    }

    public PythonExecuteViewModel(ISettingsService settingsService) : base(settingsService)
    {
        LoadSavedGroups();
        RefreshFilteredItems();
    }

    [RelayCommand]
    private void CreateGroup()
    {
        var defaultGroup = $"Group {AllGroups.Count + 1}";
        
        var groupName = Microsoft.VisualBasic.Interaction.InputBox(
            "Rename Group",
            "Enter new group name:",
            defaultGroup);

        var group = new PyGroupNode(groupName);
        AllGroups.Add(group);
        RefreshFilteredItems();
        UpdateGroupsToConfig();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteScript))]
    private void ExecuteScript(object? parameter = null)
    {
        var itemToExecute = parameter ?? SelectedItem;
        if (itemToExecute is not PyScriptNode script) return;
        
        LastExecutedItem = script;
        
        var dynFilePath = script.DynamoScriptItem.Create();
        
        if (string.IsNullOrEmpty(dynFilePath))
        {
            Trace.TraceError($"Failed to create Dynamo file for script: {script.FilePath}");
            return;
        }
        
        DynamoEngine.RunDynamoGraph(dynFilePath);
    }

    private bool CanExecuteScript(object? parameter = null)
    {
        var item = parameter ?? SelectedItem;
        return item is PyScriptNode;
    }

    [RelayCommand]
    private void RemoveItem()
    {
        switch (SelectedItem)
        {
            case null:
                return;
            case var currentNode:
                currentNode.IsSelected = false;
                if (currentNode is PyScriptNode script && script == LastExecutedItem)
                {
                    LastExecutedItem = null;
                }
                break;
        }

        var nextSelection = SelectedItem switch
        {
            PyGroupNode group => HandleRemoveGroup(group),
            PyScriptNode script => HandleRemoveScript(script),
            _ => null
        };

        RefreshFilteredItems();
        UpdateGroupsToConfig();

        SelectedItem = nextSelection;
        if (nextSelection is not null)
            nextSelection.IsSelected = true;
    }

    private PyGroupNode? HandleRemoveGroup(PyGroupNode group)
    {
        var index = AllGroups.IndexOf(group);
        AllGroups.Remove(group);

        // Select next/previous group
        if (index < AllGroups.Count)
            return AllGroups[index];
        return index > 0 ? AllGroups[index - 1] : null;
    }

    private TreeNodeBase? HandleRemoveScript(PyScriptNode script)
    {
        foreach (var group in AllGroups)
        {
            if (!group.Children.Contains(script)) continue;

            var index = group.Children.IndexOf(script);
            group.Children.Remove(script);

            // If group becomes empty, remove it too
            return group.Children.Count == 0 ? HandleRemoveGroup(group) :
                // Select next/previous script in same group
                SelectSiblingOrParent(group.Children, index, group);
        }
        return null;
    }

    private static TreeNodeBase SelectSiblingOrParent(
        ObservableCollection<TreeNodeBase> siblings,
        int removedIndex,
        TreeNodeBase parent)
    {
        if (siblings.Count == 0) return parent;
        var newIndex = Math.Min(removedIndex, siblings.Count - 1);
        return siblings[newIndex];
    }

    [RelayCommand]
    protected override void Clear()
    {
        AllGroups.Clear();
        RefreshFilteredItems();
        UpdateGroupsToConfig();
    }

    [RelayCommand(CanExecute = nameof(CanRenameGroup))]
    private void DoRenameGroup(object? parameter = null)
    {
        var itemToRename = parameter ?? SelectedItem;
        if (itemToRename is not PyGroupNode group) return;
        
        var newName = Microsoft.VisualBasic.Interaction.InputBox(
            "Rename Group",
            "Enter new group name:",
            group.GroupName);

        if (string.IsNullOrWhiteSpace(newName) || newName == group.GroupName) return;
        group.GroupName = newName;
        RefreshFilteredItems();
        UpdateGroupsToConfig();
    }

    private bool CanRenameGroup(object? parameter = null)
    {
        var item = parameter ?? SelectedItem;
        return item is PyGroupNode;
    }

    [RelayCommand(CanExecute = nameof(CanAddScript))]
    private void DoAddNewScript(object? parameter = null)
    {
        var itemToAddTo = parameter ?? SelectedItem;
        if (itemToAddTo is not PyGroupNode group) return;

        var scriptItem = new DynamoScriptItem();
        var filePath = scriptItem.CreatePythonFile();

        if (!string.IsNullOrEmpty(filePath))
        {
            group.Children.Add(new PyScriptNode(filePath));
            RefreshFilteredItems();
            UpdateGroupsToConfig();
        }
    }

    private bool CanAddScript(object? parameter = null)
    {
        var item = parameter ?? SelectedItem;
        return item is PyGroupNode;
    }

    [RelayCommand(CanExecute = nameof(CanSelectScript))]
    private void DoSelectScript(object? parameter = null)
    {
        var itemToAddTo = parameter ?? SelectedItem;
        if (itemToAddTo is not PyGroupNode group) return;

        var dialog = new OpenFileDialog
        {
            Filter = "Python files (*.py)|*.py|All files (*.*)|*.*",
            Title = "Select Python Script"
        };

        if (dialog.ShowDialog() == true)
        {
            // Check if script already exists in this group
            var existingScript = group.Children.OfType<PyScriptNode>()
                .FirstOrDefault(s => s.FilePath == dialog.FileName);

            if (existingScript == null)
            {
                group.Children.Add(new PyScriptNode(dialog.FileName));
                RefreshFilteredItems();
                UpdateGroupsToConfig();
            }
            else
            {
                Trace.TraceWarning($"Script already exists in group: {dialog.FileName}");
            }
        }
    }

    private bool CanSelectScript(object? parameter = null)
    {
        var item = parameter ?? SelectedItem;
        return item is PyGroupNode;
    }

    protected override IEnumerable<TreeNodeBase> GetAllItems()
    {
        return AllGroups;
    }

    protected override IEnumerable<TreeNodeBase> FilterItemsCore(string searchText)
    {
        var filtered = new List<TreeNodeBase>();
        var searchLower = searchText.ToLowerInvariant();

        foreach (var group in AllGroups)
        {
            var filteredGroup = FilterGroup(group, searchLower);
            if (filteredGroup != null)
            {
                filtered.Add(filteredGroup);
            }
        }

        return filtered;
    }

    protected override string? GetFilePathFromSelectedItem()
    {
        return SelectedItem switch
        {
            PyScriptNode script => script.FilePath,
            _ => null
        };
    }

    private PyGroupNode? FilterGroup(PyGroupNode group, string searchLower)
    {
        if (group.DisplayName.ToLowerInvariant().Contains(searchLower))
        {
            return CreateGroupWithAllScripts(group, searchLower);
        }

        return CreateGroupWithFilteredScripts(group, searchLower);
    }

    private PyGroupNode CreateGroupWithAllScripts(PyGroupNode group, string searchLower)
    {
        var result = new PyGroupNode(group.GroupName) { IsExpanded = true };
        ApplySearchHighlight(result, searchLower);

        // Include all scripts
        foreach (var script in group.Children.OfType<PyScriptNode>())
        {
            result.Children.Add(new PyScriptNode(script.FilePath));
        }
        return result;
    }

    private PyGroupNode? CreateGroupWithFilteredScripts(PyGroupNode group, string searchLower)
    {
        var filteredScripts = new List<PyScriptNode>();
        foreach (var script in group.Children.OfType<PyScriptNode>())
        {
            if (!script.DisplayName.ToLowerInvariant().Contains(searchLower)) continue;
            
            var clonedScript = new PyScriptNode(script.FilePath);
            ApplySearchHighlight(clonedScript, searchLower);
            filteredScripts.Add(clonedScript);
        }

        if (filteredScripts.Count == 0)
            return null;

        var filteredResult = new PyGroupNode(group.GroupName) { IsExpanded = true };
        foreach (var script in filteredScripts)
        {
            filteredResult.Children.Add(script);
        }

        return filteredResult;
    }

    private void LoadSavedGroups()
    {
        var config = SettingsService.CodeExecuteConfig;
        foreach (var groupConfig in config.PythonGroups)
        {
            var group = new PyGroupNode(groupConfig.Name);
            foreach (var scriptPath in groupConfig.Scripts)
            {
                if (System.IO.File.Exists(scriptPath))
                {
                    group.Children.Add(new PyScriptNode(scriptPath));
                }
            }
            if (group.Children.Count > 0 || groupConfig.Scripts.Count == 0)
            {
                AllGroups.Add(group);
            }
        }
    }

    private void UpdateGroupsToConfig()
    {
        var config = SettingsService.CodeExecuteConfig;
        config.PythonGroups = AllGroups.Select(g => new PythonGroup
        {
            Name = g.GroupName,
            Scripts = g.Children.OfType<PyScriptNode>().Select(s => s.FilePath).ToList()
        }).ToList();
    }
}
