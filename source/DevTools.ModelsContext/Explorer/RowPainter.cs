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

    private static HideState StateOf(int hidden, int total)
    {
        if (hidden <= 0)
        {
            return HideState.Shown;
        }

        return hidden >= total ? HideState.Hidden : HideState.Partly;
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
            case TreeRowKind.CurrentModelHeader when document is not null:
                ApplyHeaderState(row, document, stale);
                break;
            case TreeRowKind.LinkModel when document is not null:
                ApplyModelState(row, document, stale);
                break;
            case TreeRowKind.Group or TreeRowKind.Family or TreeRowKind.Type when document is not null:
                ApplyBranchState(row, document, stale);
                break;
            case TreeRowKind.Instance when document is not null:
                ApplyInstanceState(row, document, stale);
                break;
            default:
                row.HideState = HideState.None;
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
        row.HideState = stale
            ? HideState.Unknown
            : StateOf(state.Hidden.Count, scope.Counts.Count + state.HiddenElsewhere.Count - state.NotDrawn.Count);
    }

    private void ApplyHeaderState(TreeRow row, DocumentScope scope, bool stale)
    {
        var snapshot = scope.Snapshot;
        var state = states[scope.Key];
        row.Branches = RowFactory.KeysOf(scope.Counts, state.Hidden, state.Dimmed)
            .Where(index => categories.Lists(snapshot.Groups[index].GroupId))
            .OrderBy(index => index)
            .Select(RowPath.ForGroup)
            .ToList();
        var depth = snapshot.GroupingDepth;
        ApplyPathsState(row, scope, stale, [.. row.Branches.Select(path => RowFactory.KeyPath(path, depth))]);
    }

    private void ApplyBranchState(TreeRow row, DocumentScope scope, bool stale)
    {
        var depth = scope.Snapshot.GroupingDepth;
        var paths = row.Subgroups is { } subgroups
            ? subgroups.Select(subgroup => RowFactory.KeyPath(subgroup.Path, depth)).Prepend(RowFactory.KeyPath(row.Path, depth)).ToList()
            : [RowFactory.KeyPath(row.Path, depth)];
        ApplyPathsState(row, scope, stale, paths);
    }

    private void ApplyPathsState(TreeRow row, DocumentScope scope, bool stale, List<int[]> paths)
    {
        var branch = states[scope.Key];
        var hidden = paths.Sum(branch.Hidden.CountAt);
        row.Count = paths.Sum(scope.Counts.CountAt);
        row.SelectedInside = paths.Sum(branch.Selected.CountAt);
        row.DimmedCount = paths.Sum(branch.Dimmed.CountAt);
        row.IsDimmed = row.Count == 0 && hidden == 0 && row.DimmedCount > 0;
        row.HideState = row.IsDimmed
            ? HideState.None
            : InheritedState(row, stale)
              ?? StateOf(hidden, row.Count + paths.Sum(branch.HiddenElsewhere.CountAt) - paths.Sum(branch.NotDrawn.CountAt));
    }

    private void ApplyInstanceState(TreeRow row, DocumentScope scope, bool stale)
    {
        var state = states[scope.Key];
        row.SelectedInside = selection.Elements.Contains(new ElementRef(scope.Key, row.Id)) ? 1 : 0;
        row.IsDimmed = state.DimmedIds.Contains(row.Id);
        row.HideState = InheritedState(row, stale)
                  ?? (row.IsDimmed || state.HiddenIds.Contains(row.Id) ? HideState.Hidden : HideState.Shown);
    }

    private HideState? InheritedState(TreeRow row, bool stale)
    {
        if (GroupIdOf(row) is { } group && visibility.HidesCategory(group))
        {
            return HideState.HiddenByCategory;
        }

        return stale && row.IsInsideLink ? HideState.Unknown : null;
    }
}
