using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Selection;

public sealed record PickResult(IReadOnlyCollection<ElementRef> Picked, bool Cancelled)
{
    public static PickResult Cancel { get; } = new([], true);

    public int Count => Picked.Count;

    public static PickResult Of(IReadOnlyCollection<ElementRef> picked)
    {
        return new PickResult(picked, false);
    }
}
