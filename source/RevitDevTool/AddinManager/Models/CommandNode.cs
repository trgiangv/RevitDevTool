using System.Collections.ObjectModel;

namespace RevitDevTool.AddinManager.Models;

/// <summary>
/// Represents an executable command in the tree hierarchy (leaf node)
/// </summary>
public class CommandNode(AddinItem addinItem) : TreeNodeBase
{
    public AddinItem AddinItem { get; } = addinItem;

    public override string DisplayName => AddinItem.Name;
    
    public override ObservableCollection<TreeNodeBase> Children { get; } = []; // Leaf node - no children
}
