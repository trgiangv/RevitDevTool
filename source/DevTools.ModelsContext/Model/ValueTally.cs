namespace DevTools.ModelsContext.Model;

public sealed class ValueTally
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NumberRange> _stored = new(StringComparer.Ordinal);

    public bool HasMore { get; private set; }

    public int NoParameterCount { get; private set; }

    public void Add(ValueIndex index)
    {
        HasMore |= index.HasMore;
        NoParameterCount += index.NoParameterCount;
        foreach (var value in index.Values)
        {
            _counts[value.Value] = CountOf(value.Value) + value.Count;
            if (value.Stored is { } range)
            {
                _stored[value.Value] = _stored.TryGetValue(value.Value, out var known) ? known.Union(range) : range;
            }
        }
    }

    public IReadOnlyList<ValueCount> YesNo()
    {
        return [new ValueCount(ValueKinds.Yes, CountOf(ValueKinds.Yes)), new ValueCount(ValueKinds.No, CountOf(ValueKinds.No))];
    }

    public IReadOnlyList<ValueCount> Top(int max)
    {
        return _counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.CurrentCultureIgnoreCase)
            .Take(max)
            .Select(pair => new ValueCount(pair.Key, pair.Value, _stored.TryGetValue(pair.Key, out var range) ? range : null))
            .ToList();
    }

    public bool IsTruncated(int max)
    {
        return HasMore || _counts.Count > max;
    }

    private int CountOf(string value)
    {
        return _counts.TryGetValue(value, out var count) ? count : 0;
    }
}
