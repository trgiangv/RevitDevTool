using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Explorer;

internal sealed class RowPainter(
    VisibilityController visibility,
    DocumentStates states,
    ExplorerTree tree,
    ExplorerSelection selection,
    ExplorerCategories categories)
{
    private readonly RowOverrides _overrides = new(visibility, states);

    public void Apply(TreeRow row)
    {
        var document = tree.DocumentOf(row);
        if (document is not null)
        {
            row.DisabledReason = document.Info.IsLoaded ? null : VisibilityText.NotLoaded;
        }

        var stale = visibility.Journal.IsStale;
        ApplyKindState(row, document, stale);
        _overrides.Apply(row, document, stale);
        if ((row.Children ?? row.Subgroups) is not { } children)
        {
            return;
        }

        foreach (var child in children)
        {
            Apply(child);
        }
    }

    private static RowEye EyeOf(int hidden, int total)
    {
        if (hidden <= 0)
        {
            return RowEye.Shown;
        }

        return hidden >= total ? RowEye.Hidden : RowEye.Partly;
    }

    private static long? GroupIdOf(TreeRow row)
    {
        for (var current = row; current is not null; current = current.Parent)
        {
            if (current.Kind == TreeRowKind.Group)
            {
                return current.Id;
            }
        }

        return null;
    }

    private void ApplyKindState(TreeRow row, DocumentScope? document, bool stale)
    {
        switch (row.Kind)
        {
            case TreeRowKind.CurrentModelHeader or TreeRowKind.LinkModel when document is not null:
                ApplyModelState(row, document, stale);
                break;
            case TreeRowKind.Group or TreeRowKind.Family or TreeRowKind.Type when document is not null:
                ApplyBranchState(row, document, stale);
                break;
            case TreeRowKind.Instance when document is not null:
                ApplyInstanceState(row, document, stale);
                break;
            default:
                row.Eye = RowEye.None;
                break;
        }
    }

    private void ApplyModelState(TreeRow row, DocumentScope scope, bool stale)
    {
        var state = states[scope.Key];
        var snapshot = scope.Snapshot;
        row.Count = scope.Counts.Children.Values
            .Where(group => categories.Lists(snapshot.Groups[group.Key].GroupId))
            .Sum(group => group.Count);
        row.SelectedInside = state.Selected.Count;
        row.Eye = ModelEye(row, scope, state, stale);
    }

    private static RowEye ModelEye(TreeRow row, DocumentScope scope, DocumentState state, bool stale)
    {
        if (row.IsHeader)
        {
            return RowEye.None;
        }

        return stale ? RowEye.Unknown : EyeOf(state.Hidden.Count, scope.Counts.Count + state.Outside.Count);
    }

    private void ApplyBranchState(TreeRow row, DocumentScope scope, bool stale)
    {
        var depth = scope.Snapshot.GroupingDepth;
        var paths = row.Subgroups is { } subgroups
            ? subgroups.Select(subgroup => RowFactory.KeyPath(subgroup.Path, depth)).Prepend(RowFactory.KeyPath(row.Path, depth)).ToList()
            : [RowFactory.KeyPath(row.Path, depth)];
        var branch = states[scope.Key];
        var hidden = paths.Sum(branch.Hidden.CountAt);
        row.Count = paths.Sum(scope.Counts.CountAt);
        row.SelectedInside = paths.Sum(branch.Selected.CountAt);
        row.HiddenCount = paths.Sum(branch.Unseen.CountAt);
        row.IsHiddenOpen = tree.IsHiddenOpen(row);
        row.IsDimmed = row.Count == 0 && hidden == 0 && row.HiddenCount > 0;
        row.Eye = row.IsDimmed
            ? RowEye.None
            : InheritedEye(row, stale) ?? EyeOf(hidden, row.Count + paths.Sum(branch.Outside.CountAt));
    }

    private void ApplyInstanceState(TreeRow row, DocumentScope scope, bool stale)
    {
        var state = states[scope.Key];
        row.SelectedInside = selection.Elements.Contains(new ElementRef(scope.Key, row.Id)) ? 1 : 0;
        row.IsDimmed = state.UnseenIds.Contains(row.Id);
        row.Eye = row.IsDimmed
            ? RowEye.None
            : InheritedEye(row, stale) ?? (state.HiddenIds.Contains(row.Id) ? RowEye.Hidden : RowEye.Shown);
    }

    private RowEye? InheritedEye(TreeRow row, bool stale)
    {
        if (GroupIdOf(row) is { } group && visibility.IsCategoryHidden(group))
        {
            return RowEye.HiddenByCategory;
        }

        return stale && row.IsInsideLink ? RowEye.Unknown : null;
    }
}
