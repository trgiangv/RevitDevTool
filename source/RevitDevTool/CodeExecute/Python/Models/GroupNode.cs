using System.Collections.ObjectModel;
using RevitDevTool.CodeExecute.Shared.Models;

namespace RevitDevTool.CodeExecute.Python.Models;

/// <summary>
/// Represents a Python script group in the tree hierarchy
/// </summary>
public class GroupNode : TreeNodeBase
{
    private string _groupName;
    private string _displayName;

    /// <summary>
    /// Display name shown in the tree
    /// </summary>
    public override string DisplayName => _displayName;

    public GroupNode(string groupName)
    {
        _groupName = groupName;
        _displayName = groupName;
    }

    /// <summary>
    /// The group name
    /// </summary>
    public string GroupName
    {
        get => _groupName;
        set
        {
            if (_groupName == value) return;
            _groupName = value;
            _displayName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Collection of child nodes (Python scripts)
    /// </summary>
    public override ObservableCollection<TreeNodeBase> Children { get; } = [];
}
