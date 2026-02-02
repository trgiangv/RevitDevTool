using System.Collections.ObjectModel;
using RevitDevTool.CodeExecute.Shared.Models;

namespace RevitDevTool.CodeExecute.CSharp.Models;

/// <summary>
/// Represents a namespace in the tree hierarchy
/// </summary>
public class NamespaceNode(string namespaceName) : TreeNodeBase
{
    public string Namespace { get; } = namespaceName;

    public override string DisplayName => Namespace;

    public override ObservableCollection<TreeNodeBase> Children { get; } = [];

    /// <summary>
    /// Adds a command to this namespace
    /// </summary>
    public void AddCommand(AddinItem item)
    {
        Children.Add(new CommandNode(item));
    }
}
