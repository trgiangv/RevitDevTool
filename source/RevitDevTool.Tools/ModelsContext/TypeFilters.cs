using DevTools.ModelsContext.Engine;
using Nice3point.Revit.Extensions;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class TypeFilters(Func<TypeMembership> members)
{
    private TypeMembership? _members;

    public Compiled In(IReadOnlyCollection<long> types)
    {
        if (types.Count == 0)
        {
            return Compiled.None;
        }

        var split = Members.Split(types);
        if (split.Inside.Count == 0)
        {
            return Compiled.None;
        }

        return split.Outside.Count == 0 ? Compiled.All : Exclude(split.Outside);
    }

    public Compiled NotIn(IReadOnlyCollection<long> types)
    {
        if (types.Count == 0)
        {
            return Compiled.All;
        }

        var split = Members.Split(types);
        if (split.Inside.Count == 0)
        {
            return Compiled.All;
        }

        return split.Outside.Count == 0 ? Compiled.None : Exclude(split.Inside);
    }

    private TypeMembership Members => _members ??= members();

    private static Compiled Exclude(IReadOnlyList<long> ids)
    {
        return new Compiled.Filter(new ExclusionFilter(ids.Select(id => id.ToElementId()).ToList()));
    }
}
