using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Explorer;

internal sealed class ExplorerRows(
    PanelRuntime runtime,
    VisibilityController visibility,
    DocumentStates states,
    ExplorerTree tree,
    ExplorerSearch search,
    ExplorerSelection selection,
    ExplorerCategories categories)
{
    private readonly RowPainter _painter = new(visibility, states, tree, selection, categories);

    public RangeObservableCollection<TreeRow> Items { get; } = [];

    public async Task ExpandAsync(TreeRow row)
    {
        if (row.IsExpanded || !row.HasChildren)
        {
            return;
        }

        var timer = TraceTimer.Start(runtime.Logger);
        await tree.EnsureChildrenAsync(row);
        row.IsExpanded = true;
        _painter.Apply(row);
        var index = Items.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        var visible = search.VisibleChildren(row);
        Items.InsertRange(index + 1, visible);
        if (timer.IsEnabled)
        {
            runtime.Logger.RowExpanded(row.Text, visible.Count, timer.ElapsedMs);
        }

        await RepublishIfHiddenChangedAsync();
    }

    public void Collapse(TreeRow row)
    {
        if (!row.IsExpanded)
        {
            return;
        }

        row.IsExpanded = false;
        var index = Items.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        var end = index + 1;
        while (end < Items.Count && Items[end].Level > row.Level)
        {
            end++;
        }

        Items.ReplaceRange(index, end - index, [row]);
    }

    public async Task ExpandAllAsync()
    {
        var timer = TraceTimer.Start(runtime.Logger);
        foreach (var root in tree.Roots)
        {
            await tree.ExpandBranchAsync(root);
        }

        await ReadHiddenAsync();
        Publish();
        if (timer.IsEnabled)
        {
            runtime.Logger.RowExpanded(ExplorerTabViewModel.ExpandAllText, Items.Count, timer.ElapsedMs);
        }
    }

    public void CollapseAll()
    {
        tree.CollapseAll();
        Publish();
    }

    public async Task ToggleHiddenAsync(TreeRow row)
    {
        await tree.SetHiddenOpenAsync(row, !tree.IsHiddenOpen(row));
        if (row.HasChildren && !row.IsExpanded)
        {
            await tree.EnsureChildrenAsync(row);
            row.IsExpanded = true;
        }

        Publish();
    }

    public async Task LoadMoreAsync(TreeRow? more)
    {
        if (more is not { Kind: TreeRowKind.MorePage, Parent: { } parent })
        {
            return;
        }

        var page = await tree.AppendPageAsync(parent);
        foreach (var row in page)
        {
            _painter.Apply(row);
        }

        var index = Items.IndexOf(more);
        if (index >= 0)
        {
            Items.ReplaceRange(index, 1, page);
        }

        await RepublishIfHiddenChangedAsync();
    }

    public async Task<bool> ReadHiddenAsync()
    {
        var changed = await visibility.ReadBackAsync(tree.Token);
        if (changed)
        {
            ComputeStates();
        }

        return changed;
    }

    public void ComputeStates()
    {
        states.Compute(runtime.Data, selection.Elements);
    }

    public void ComputeSelected()
    {
        states.ComputeSelected(runtime.Data, selection.Elements);
    }

    public void Publish()
    {
        var timer = TraceTimer.Start(runtime.Logger);
        foreach (var root in tree.Roots)
        {
            _painter.Apply(root);
        }

        var visible = search.Visible();
        var rebuilt = !Items.SequenceEqual(visible);
        if (rebuilt)
        {
            Items.ReplaceAll(visible);
        }

        if (timer.IsEnabled)
        {
            runtime.Logger.RowsPublished(visible.Count, rebuilt, timer.ElapsedMs);
        }
    }

    private async Task RepublishIfHiddenChangedAsync()
    {
        if (await ReadHiddenAsync())
        {
            Publish();
        }
    }
}
