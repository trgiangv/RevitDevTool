using System.Collections.ObjectModel;

namespace RevitDevTool.CodeExecute.Shared.Models;

/// <summary>
/// Base class for all tree nodes in the Add-in hierarchy
/// </summary>
public abstract partial class TreeNodeBase : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private ISelectionRange? _highlightRange;

    /// <summary>
    /// Display name shown in the tree
    /// </summary>
    public abstract string DisplayName { get; }
    
    /// <summary>
    /// Child nodes
    /// </summary>
    public abstract ObservableCollection<TreeNodeBase> Children { get; }

    /// <summary>
    /// Whether this node has children
    /// </summary>
    public bool HasChildren => Children.Count > 0;

    public override string ToString() => DisplayName;
}
