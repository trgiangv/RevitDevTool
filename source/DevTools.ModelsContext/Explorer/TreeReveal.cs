using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;

namespace DevTools.ModelsContext.Explorer;

internal sealed class TreeReveal(PanelRuntime runtime, ExplorerTree tree)
{
    public async Task<List<TreeRow>> RevealAsync(IEnumerable<ElementRef> elements, int limit)
    {
        var rows = new List<TreeRow>();
        foreach (var element in elements.Take(limit).ToList())
        {
            if (await RevealAsync(element) is { } row)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    public async Task<TreeRow?> RevealAsync(ElementRef element)
    {
        if (runtime.Data.Find(element.Document) is not { } scope || !scope.Snapshot.TryGetRow(element.Id, out var target))
        {
            return null;
        }

        var row = tree.Roots.FirstOrDefault(root =>
            root.Kind is TreeRowKind.CurrentModelHeader or TreeRowKind.LinkModel && root.Document == element.Document);
        while (row is not null)
        {
            var next = await ChildTowardAsync(row, element.Id, scope, target);
            if (next is { Kind: TreeRowKind.Instance })
            {
                return next;
            }

            if (next is { Kind: TreeRowKind.Group } && tree.IsUnseen(element.Document, element.Id))
            {
                await tree.SetHiddenOpenAsync(next, true);
            }

            row = next;
        }

        return null;
    }

    private static bool LeadsTo(TreeRow child, long id, DocumentScope scope, ElementRow target)
    {
        return child.Kind switch
        {
            TreeRowKind.Instance => child.Id == id,
            TreeRowKind.MorePage => false,
            _ => scope.Snapshot.IsUnder(target, child.Path)
                 || child.Subgroups?.Any(subgroup => scope.Snapshot.IsUnder(target, subgroup.Path)) == true
        };
    }

    private static bool IsInstance(TreeRow row, long id)
    {
        return row.Kind == TreeRowKind.Instance && row.Id == id;
    }

    private static bool EndsWithMorePage(TreeRow row)
    {
        return row.Children is { Count: > 0 } children && children[children.Count - 1].Kind == TreeRowKind.MorePage;
    }

    private async Task<TreeRow?> ChildTowardAsync(TreeRow row, long id, DocumentScope scope, ElementRow target)
    {
        await tree.EnsureChildrenAsync(row);
        row.IsExpanded = true;
        var next = row.Children!.FirstOrDefault(child => LeadsTo(child, id, scope, target));
        while (next is null && EndsWithMorePage(row))
        {
            var page = await tree.AppendPageAsync(row);
            next = page.FirstOrDefault(child => IsInstance(child, id));
        }

        return next;
    }
}
