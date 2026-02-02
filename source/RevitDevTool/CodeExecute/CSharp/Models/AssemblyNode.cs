using System.Collections.ObjectModel;
using System.IO;
using RevitDevTool.CodeExecute.Shared.Models;

namespace RevitDevTool.CodeExecute.CSharp.Models;

/// <summary>
/// Represents a DLL assembly in the tree hierarchy
/// </summary>
public class AssemblyNode(string filePath) : TreeNodeBase
{
    public string FilePath { get; } = filePath;

    public override string DisplayName => Path.GetFileName(FilePath);

    public override ObservableCollection<TreeNodeBase> Children { get; } = [];

    /// <summary>
    /// Groups commands by namespace and adds them as children
    /// </summary>
    public void GroupByNamespace(IEnumerable<AddinItem> items)
    {
        Children.Clear();

        var grouped = items.GroupBy(i => GetNamespace(i.FullClassName))
                          .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var namespaceNode = new NamespaceNode(group.Key);

            foreach (var item in group.OrderBy(i => i.Name))
            {
                namespaceNode.AddCommand(item);
            }

            Children.Add(namespaceNode);
        }
    }

    private static string GetNamespace(string fullClassName)
    {
        var lastDotIndex = fullClassName.LastIndexOf('.');
        return lastDotIndex > 0 ? fullClassName[..lastDotIndex] : "(Global)";
    }
}
