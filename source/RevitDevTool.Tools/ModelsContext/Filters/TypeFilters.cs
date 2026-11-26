using DevTools.ModelsContext.Filters;
using Nice3point.Revit.Extensions;

namespace RevitDevTool.Tools.ModelsContext.Filters;

internal sealed class TypeFilters(Func<TypeMembership> members)
{
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

    public Compiled Only(IReadOnlyCollection<long> ids)
    {
        if (ids.Count == 0)
        {
            return Compiled.None;
        }

        var split = Members.SplitIds(ids);
        if (split.Inside.Count == 0)
        {
            return Compiled.None;
        }

        return split.Outside.Count == 0 ? Compiled.All : Exclude(split.Outside);
    }

    public Compiled Except(IReadOnlyCollection<long> ids)
    {
        return ids.Count == 0 ? Compiled.All : Exclude([.. ids]);
    }

    private TypeMembership Members => field ??= members();

    private static Compiled Exclude(IReadOnlyList<long> ids)
    {
        return new Compiled.Filter(new ExclusionFilter(ids.Select(id => id.ToElementId()).ToList()));
    }
}
