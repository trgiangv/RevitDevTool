using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;

namespace DevTools.ModelsContext.Visibility;

public sealed record VisibilityRequest(
    VisibilityAction Action,
    IReadOnlyList<TreeRow> Rows,
    IReadOnlyList<ElementRef> Elements,
    RgbColor? Color = null,
    FilterDefinition? Filter = null)
{
    public static VisibilityRequest ForRows(VisibilityAction action, params IReadOnlyList<TreeRow> rows)
    {
        return new VisibilityRequest(action, rows, []);
    }

    public static VisibilityRequest ForElements(VisibilityAction action, IReadOnlyList<ElementRef> elements)
    {
        return new VisibilityRequest(action, [], elements);
    }
}
