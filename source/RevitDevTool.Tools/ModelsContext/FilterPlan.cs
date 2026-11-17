using Nice3point.Revit.Extensions;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class FilterPlan(Compiled native, Func<Element, bool>? exact)
{
    public Compiled Native { get; } = native;

    public bool HasExactPass => exact is not null;

    public int NativeCount { get; private set; }

    public List<long> Match(Document target, List<ElementId> chunk)
    {
        if (chunk.Count == 0)
        {
            return [];
        }

        ICollection<ElementId> ids = Native switch
        {
            Compiled.MatchNone => new List<ElementId>(),
            Compiled.Filter matcher => target.CollectElements(chunk)
                .WherePasses(matcher.Value)
                .ToElementIds(),
            _ => chunk
        };
        NativeCount += ids.Count;
        return exact is null
            ? ids.Select(id => id.ToLong()).ToList()
            : ids.Where(id => target.GetElement(id) is { } element && exact(element)).Select(id => id.ToLong()).ToList();
    }
}
