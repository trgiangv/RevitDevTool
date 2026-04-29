using DevTools.Execution.Models;
using DevTools.UI.Behaviors;
namespace DevTools.Presentation.ViewModels;

internal static class ExecutionTreeViewHelper
{
    public static void ExpandAll(IEnumerable<TreeNodeBase> roots)
    {
        foreach (var node in roots)
            SetExpandedRecursive(node, true);
    }

    public static void CollapseAll(IEnumerable<TreeNodeBase> roots)
    {
        foreach (var node in roots)
            SetExpandedRecursive(node, false);
    }

    public static void ToggleAll(IEnumerable<TreeNodeBase> roots)
    {
        foreach (var node in roots)
            ToggleRecursive(node);
    }

    public static void SetVisibilityRecursive(TreeNodeBase node, bool visible)
    {
        node.IsVisible = visible;
        foreach (var child in node.ChildNodes)
            SetVisibilityRecursive(child, visible);
    }

    public static void ClearHighlightsRecursive(TreeNodeBase node)
    {
        node.HighlightRange = null;
        foreach (var child in node.ChildNodes)
            ClearHighlightsRecursive(child);
    }

    public static bool FilterNodeRecursive(TreeNodeBase node, string searchText, bool isDarkTheme)
    {
        var search = searchText.Trim();
        var searchLower = search.ToLowerInvariant();
        var nodeTextLower = node.Name.ToLowerInvariant();
        var index = searchLower.Length == 0 ? -1 : nodeTextLower.IndexOf(searchLower, StringComparison.Ordinal);
        var currentMatches = index >= 0;

        node.HighlightRange = currentMatches
            ? new HighlightRange(index, index + search.Length) { DarkSkin = isDarkTheme }
            : null;

        if (currentMatches)
            node.IsExpanded = true;

        var childrenMatch = false;
        foreach (var child in node.ChildNodes)
        {
            if (!FilterNodeRecursive(child, searchText, isDarkTheme))
                continue;

            childrenMatch = true;
            node.IsExpanded = true;
        }

        node.IsVisible = currentMatches || childrenMatch;
        return node.IsVisible;
    }

    private static void SetExpandedRecursive(TreeNodeBase node, bool expanded)
    {
        node.IsExpanded = expanded;
        foreach (var child in node.ChildNodes)
            SetExpandedRecursive(child, expanded);
    }

    private static void ToggleRecursive(TreeNodeBase node)
    {
        node.IsExpanded = !node.IsExpanded;
        foreach (var child in node.ChildNodes)
            ToggleRecursive(child);
    }
}
