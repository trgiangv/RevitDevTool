using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Explorer;

internal sealed class RowOverrides(VisibilityController visibility, DocumentStates states)
{
    public void Apply(TreeRow row, DocumentScope? document, bool stale)
    {
        var own = stale ? null : OverrideOf(row, document);
        var category = !stale && row.Kind == TreeRowKind.Group ? visibility.CategoryOverrideOf(row.Id) : null;
        row.OverrideColor = own?.Color ?? category?.Color ?? row.Parent?.OverrideColor;
        row.IsHalftone = own?.Halftone == true || category?.Halftone == true || row.Parent?.IsHalftone == true;
    }

    private RowOverride? OverrideOf(TreeRow row, DocumentScope? document)
    {
        return row.Kind switch
        {
            TreeRowKind.LinkModel when row.Document is { } link =>
                visibility.OverrideOf(new ElementRef(link.CurrentModel, link.LinkInstanceId)),
            TreeRowKind.Instance when row.Document is { } key => visibility.OverrideOf(new ElementRef(key, row.Id)),
            TreeRowKind.Group or TreeRowKind.Family or TreeRowKind.Type
                when document is not null && states.TryGet(document.Key, out var state) =>
                TallyOf(row, document, state),
            _ => null
        };
    }

    private static RowOverride? TallyOf(TreeRow row, DocumentScope document, DocumentState state)
    {
        var keys = RowFactory.KeyPath(row.Path, document.Snapshot.GroupingDepth);
        return state.Overrides.At(keys, row.Subgroups is null ? row.Count : document.Counts.CountAt(keys));
    }
}
