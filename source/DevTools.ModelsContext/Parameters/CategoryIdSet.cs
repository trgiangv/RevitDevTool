using System.Collections;

namespace DevTools.ModelsContext.Parameters;

public sealed class CategoryIdSet : IReadOnlyCollection<long>, IEquatable<CategoryIdSet>
{
    public static readonly CategoryIdSet Empty = new([]);

    private readonly long[] _ids;

    private CategoryIdSet(long[] ids)
    {
        _ids = ids;
    }

    public int Count => _ids.Length;

    public static CategoryIdSet Of(IEnumerable<long> ids)
    {
        var sorted = ids.Distinct().OrderBy(id => id).ToArray();
        return sorted.Length == 0 ? Empty : new CategoryIdSet(sorted);
    }

    public bool Contains(long id)
    {
        return Array.BinarySearch(_ids, id) >= 0;
    }

    public bool Overlaps(ICollection<long> ids)
    {
        return _ids.Any(ids.Contains);
    }

    public CategoryIdSet Union(CategoryIdSet other)
    {
        if (other.Count == 0 || Equals(other))
        {
            return this;
        }

        return Count == 0 ? other : Of(_ids.Concat(other._ids));
    }

    public bool Equals(CategoryIdSet? other)
    {
        return other is not null && (ReferenceEquals(this, other) || _ids.SequenceEqual(other._ids));
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as CategoryIdSet);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var id in _ids)
            {
                hash = hash * 31 + id.GetHashCode();
            }

            return hash;
        }
    }

    public IEnumerator<long> GetEnumerator()
    {
        return ((IEnumerable<long>)_ids).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
