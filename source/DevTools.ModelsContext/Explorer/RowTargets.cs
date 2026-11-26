using DevTools.ModelsContext.Panel;

namespace DevTools.ModelsContext.Explorer;

internal sealed class RowTargets(PanelRuntime runtime, ExplorerSelection selection, ContextMenuViewModel menu)
{
    private readonly List<TreeRow> _highlighted = [];

    public void Highlight(TreeRow? row, IEnumerable<TreeRow> visibleRows)
    {
        ClearHighlight();
        if (row is null || row.Eye is RowEye.None or RowEye.HiddenByCategory)
        {
            return;
        }

        var targets = new HashSet<TreeRow>(TargetsFor(row));
        foreach (var visible in visibleRows.Where(visible => IsAffected(visible, targets)))
        {
            visible.IsHighlighted = true;
            _highlighted.Add(visible);
        }
    }

    public void ClearHighlight()
    {
        foreach (var row in _highlighted)
        {
            row.IsHighlighted = false;
        }

        _highlighted.Clear();
    }

    public List<TreeRow> TargetsFor(TreeRow row)
    {
        return selection.Rows.Contains(row) ? Actionable() : [row];
    }

    public string? ReasonFor(TreeRow row, MenuAction action, bool syncing, CancellationToken token)
    {
        return menu.ReasonFor(TargetOf(TargetsFor(row), syncing, token), action);
    }

    public void Open(bool syncing, CancellationToken token)
    {
        menu.Open(TargetOf(Actionable(), syncing, token));
    }

    private static bool IsAffected(TreeRow row, HashSet<TreeRow> targets)
    {
        for (var current = row; current is not null; current = current.Parent)
        {
            if (targets.Contains(current))
            {
                return true;
            }
        }

        return false;
    }

    private List<TreeRow> Actionable()
    {
        return selection.Rows.Where(row => row.Kind != TreeRowKind.MorePage).ToList();
    }

    private MenuTarget TargetOf(List<TreeRow> rows, bool syncing, CancellationToken token)
    {
        var hidden = rows.Count > 0 && rows.All(row => row.Eye == RowEye.Hidden);
        return new MenuTarget(rows, [], () => runtime.Data.ElementsOf(rows), syncing, hidden, token);
    }
}
