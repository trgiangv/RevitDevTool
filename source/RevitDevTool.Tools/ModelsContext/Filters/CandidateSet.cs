using DevTools.ModelsContext.Filters;
using Nice3point.Revit.Extensions;

namespace RevitDevTool.Tools.ModelsContext.Filters;

internal sealed class CandidateSet(long[] ids, HashSet<long> secondary, HashSet<long> viewOwned)
{
    private const long NoType = -1;

    private TypeMembership? _members;
    private Dictionary<long, long>? _typeOf;
    private HashSet<long>? _ids;

    public long[] Ids { get; } = ids;

    public long[] ModelIds => field ??= secondary.Count == 0 && viewOwned.Count == 0 ? Ids : Ids.Where(InModel).ToArray();

    public bool Contains(long id)
    {
        return (_ids ??= [.. Ids]).Contains(id);
    }

    public bool InModel(long id)
    {
        return !secondary.Contains(id) && !viewOwned.Contains(id);
    }

    public long[] InScope(IReadOnlyCollection<long>? scope)
    {
        if (scope is null)
        {
            return ModelIds;
        }

        var inScope = scope as ISet<long> ?? new HashSet<long>(scope);
        return Ids.Where(inScope.Contains).ToArray();
    }

    public TypeMembership MembersIn(Document document)
    {
        if (_members is { } members)
        {
            return members;
        }

        var typeIds = new long[Ids.Length];
        for (var index = 0; index < Ids.Length; index++)
        {
            typeIds[index] = TypeOf(document.GetElement(Ids[index].ToElementId()));
        }

        _typeOf = new Dictionary<long, long>(Ids.Length);
        for (var index = 0; index < Ids.Length; index++)
        {
            _typeOf[Ids[index]] = typeIds[index];
        }

        return _members = new TypeMembership(Ids, typeIds);
    }

    public bool Holds(Document document, IEnumerable<ElementId> modified)
    {
        foreach (var id in modified)
        {
            if (document.GetElement(id) is not { } element || element is ElementType)
            {
                continue;
            }

            var value = id.ToLong();
            if (secondary.Contains(value) != element.DesignOption is { IsPrimary: false })
            {
                return false;
            }

            if (_typeOf is { } typeOf && typeOf.TryGetValue(value, out var typeId) && typeId != TypeOf(element))
            {
                return false;
            }
        }

        return true;
    }

    private static long TypeOf(Element? element)
    {
        return element?.GetTypeId() is { } typeId && typeId != ElementId.InvalidElementId
            ? typeId.ToLong()
            : NoType;
    }
}
