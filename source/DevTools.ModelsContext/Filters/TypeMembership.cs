namespace DevTools.ModelsContext.Filters;

public sealed record TypePresence(IReadOnlyCollection<long> WithValue, IReadOnlyCollection<long> WithoutValue)
{
    public static TypePresence None { get; } = new([], []);

    public bool IsEmpty => WithValue.Count == 0 && WithoutValue.Count == 0;

    public IReadOnlyCollection<long> TypesFor(FilterOperator op)
    {
        return op switch
        {
            FilterOperator.NotEmpty => WithValue,
            FilterOperator.Empty => WithoutValue,
            _ => [.. WithValue, .. WithoutValue]
        };
    }
}

public readonly record struct MemberSplit(IReadOnlyList<long> Inside, IReadOnlyList<long> Outside);

public sealed class TypeMembership
{
    private readonly long[] _candidates;
    private readonly long[] _typeIds;

    public TypeMembership(long[] candidates, long[] typeIds)
    {
        if (candidates.Length != typeIds.Length)
        {
            throw new ArgumentException("Each candidate needs one type id.", nameof(typeIds));
        }

        _candidates = candidates;
        _typeIds = typeIds;
    }

    public int Count => _candidates.Length;

    public MemberSplit Split(IReadOnlyCollection<long> types)
    {
        var set = types as ISet<long> ?? new HashSet<long>(types);
        var inside = new List<long>();
        var outside = new List<long>();
        for (var index = 0; index < _candidates.Length; index++)
        {
            (set.Contains(_typeIds[index]) ? inside : outside).Add(_candidates[index]);
        }

        return new MemberSplit(inside, outside);
    }

    public MemberSplit SplitIds(IReadOnlyCollection<long> ids)
    {
        var set = ids as ISet<long> ?? new HashSet<long>(ids);
        var inside = new List<long>();
        var outside = new List<long>();
        foreach (var candidate in _candidates)
        {
            (set.Contains(candidate) ? inside : outside).Add(candidate);
        }

        return new MemberSplit(inside, outside);
    }
}
