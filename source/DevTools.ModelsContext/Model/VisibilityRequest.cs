namespace DevTools.ModelsContext.Model;

public enum VisibilityAction
{
    Show,
    Hide,
    Isolate,
    Halftone,
    Color
}

public readonly record struct RgbColor(byte R, byte G, byte B);

public sealed record VisibilityRequest(
    VisibilityAction Action,
    IReadOnlyList<TreeRow> Rows,
    IReadOnlyList<ElementRef> Elements,
    RgbColor? Color = null)
{
    public static VisibilityRequest ForRows(VisibilityAction action, params TreeRow[] rows)
    {
        return new VisibilityRequest(action, rows, []);
    }

    public static VisibilityRequest ForElements(VisibilityAction action, IReadOnlyList<ElementRef> elements)
    {
        return new VisibilityRequest(action, [], elements);
    }
}
