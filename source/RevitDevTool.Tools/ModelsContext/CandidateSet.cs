using DevTools.ModelsContext.Engine;
using Nice3point.Revit.Extensions;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class CandidateSet(long[] ids, HashSet<long> secondary)
{
    private const long NoType = -1;

    private TypeMembership? _members;
    private long[]? _modelIds;

    public long[] Ids { get; } = ids;

    public long[] ModelIds => _modelIds ??= secondary.Count == 0 ? Ids : Ids.Where(id => !secondary.Contains(id)).ToArray();

    public bool InModel(long id)
    {
        return !secondary.Contains(id);
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
            typeIds[index] = document.GetElement(Ids[index].ToElementId())?.GetTypeId() is { } typeId
                             && typeId != ElementId.InvalidElementId
                ? typeId.ToLong()
                : NoType;
        }

        return _members = new TypeMembership(Ids, typeIds);
    }
}
