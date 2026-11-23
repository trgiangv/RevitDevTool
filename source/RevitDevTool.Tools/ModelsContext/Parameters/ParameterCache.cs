using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using RevitDevTool.Tools.ModelsContext.Filters;
using DocumentVersion = DevTools.ModelsContext.Documents.DocumentVersion;

namespace RevitDevTool.Tools.ModelsContext.Parameters;

internal sealed record ValueEntry(
    int Total,
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyDictionary<string, long[]> RawValues,
    IReadOnlyDictionary<string, NumberRange> Numbers);

internal sealed class ParameterCache
{
    private readonly Dictionary<CandidateKey, Stamped<CandidateSet>> _candidates = [];
    private readonly Dictionary<SourceId, Stamped<CatalogEntry>> _catalogs = [];
    private readonly object _gate = new();
    private readonly Dictionary<ValueKey, Stamped<IReadOnlyList<ResolvedParameter>>> _resolved = [];
    private readonly Dictionary<ValueKey, Stamped<ValueEntry>> _values = [];

    public IReadOnlyList<ParameterInfo>? FindCatalog(SourceId source, DocumentVersion? version)
    {
        lock (_gate)
        {
            return _catalogs.TryGetValue(source, out var entry) && Same(entry.Version, version) ? entry.Value.Catalog : null;
        }
    }

    public IReadOnlyCollection<long>? FindIds(SourceId source, DocumentVersion? version, ParameterKey key)
    {
        lock (_gate)
        {
            return _catalogs.TryGetValue(source, out var entry) && Same(entry.Version, version)
                                                                 && entry.Value.Ids.TryGetValue(key, out var ids)
                ? ids
                : null;
        }
    }

    public void StoreCatalog(
        SourceId source,
        DocumentVersion? version,
        IReadOnlyList<ParameterInfo> catalog,
        IReadOnlyDictionary<ParameterKey, IReadOnlyCollection<long>> ids)
    {
        lock (_gate)
        {
            _catalogs[source] = new Stamped<CatalogEntry>(version, new CatalogEntry(catalog, ids));
        }
    }

    public ValueEntry? FindValues(SourceId source, ParameterKey parameter, IEnumerable<long> groups, DocumentVersion? version)
    {
        lock (_gate)
        {
            return _values.TryGetValue(KeyOf(source, parameter, groups), out var entry) && Same(entry.Version, version)
                ? entry.Value
                : null;
        }
    }

    public void StoreValues(SourceId source, ParameterKey parameter, IEnumerable<long> groups, DocumentVersion? version,
        ValueEntry values)
    {
        lock (_gate)
        {
            _values[KeyOf(source, parameter, groups)] = new Stamped<ValueEntry>(version, values);
        }
    }

    public IReadOnlyList<ResolvedParameter>? FindResolved(
        SourceId source,
        ParameterKey parameter,
        IEnumerable<long> groups,
        DocumentVersion? version)
    {
        lock (_gate)
        {
            return _resolved.TryGetValue(KeyOf(source, parameter, groups), out var entry) && Same(entry.Version, version)
                ? entry.Value
                : null;
        }
    }

    public void StoreResolved(
        SourceId source,
        ParameterKey parameter,
        IEnumerable<long> groups,
        DocumentVersion? version,
        IReadOnlyList<ResolvedParameter> resolved)
    {
        lock (_gate)
        {
            _resolved[KeyOf(source, parameter, groups)] = new Stamped<IReadOnlyList<ResolvedParameter>>(version, resolved);
        }
    }

    public CandidateSet CandidatesOf(
        SourceId source,
        IEnumerable<long> groups,
        DocumentVersion? version,
        Func<CandidateSet> collect,
        out bool cached)
    {
        var key = new CandidateKey(source, new GroupSet(groups));
        lock (_gate)
        {
            if (_candidates.TryGetValue(key, out var entry) && Same(entry.Version, version))
            {
                cached = true;
                return entry.Value;
            }
        }

        var candidates = collect();
        lock (_gate)
        {
            _candidates[key] = new Stamped<CandidateSet>(version, candidates);
        }

        cached = false;
        return candidates;
    }

    public void Invalidate(SourceId source)
    {
        lock (_gate)
        {
            _catalogs.Remove(source);
            DropValues(source);
        }
    }

    public void InvalidateValues(SourceId source)
    {
        lock (_gate)
        {
            DropValues(source);
        }
    }

    public void Invalidate(SourceId source, CacheDrop drop, IReadOnlyCollection<long> modified,
        Func<CandidateSet, bool> keeps)
    {
        if (drop == CacheDrop.All)
        {
            Invalidate(source);
            return;
        }

        lock (_gate)
        {
            Remove(_values, key => key.Source == source, (_, _) => true);
            Remove(_candidates, key => key.Source == source,
                (_, entry) => drop >= CacheDrop.Candidates || !keeps(entry.Value));
            Remove(_resolved, key => key.Source == source,
                (key, entry) => !ChangeClassifier.KeepsResolved(drop, entry.Value.Count, modified,
                    _candidates.TryGetValue(new CandidateKey(source, key.Groups), out var candidates)
                        ? candidates.Value.Contains
                        : null));
        }
    }

    private void DropValues(SourceId source)
    {
        Remove(_values, key => key.Source == source, (_, _) => true);
        Remove(_resolved, key => key.Source == source, (_, _) => true);
        Remove(_candidates, key => key.Source == source, (_, _) => true);
    }

    private static void Remove<TKey, TValue>(
        Dictionary<TKey, TValue> entries,
        Func<TKey, bool> owned,
        Func<TKey, TValue, bool> drop)
        where TKey : notnull
    {
        foreach (var key in entries.Where(pair => owned(pair.Key) && drop(pair.Key, pair.Value)).Select(pair => pair.Key).ToList())
        {
            entries.Remove(key);
        }
    }

    private static bool Same(DocumentVersion? stored, DocumentVersion? current)
    {
        return stored is { } left ? current is { } right && left.IsSameAs(right) : current is null;
    }

    private static ValueKey KeyOf(SourceId source, ParameterKey parameter, IEnumerable<long> groups)
    {
        return new ValueKey(source, parameter, new GroupSet(groups));
    }

    private readonly record struct ValueKey(SourceId Source, ParameterKey Parameter, GroupSet Groups);

    private readonly record struct CandidateKey(SourceId Source, GroupSet Groups);

    private readonly struct GroupSet : IEquatable<GroupSet>
    {
        private readonly long[] _ids;
        private readonly int _hash;

        public GroupSet(IEnumerable<long> groups)
        {
            var ids = groups.ToArray();
            Array.Sort(ids);
            var count = 0;
            for (var i = 0; i < ids.Length; i++)
            {
                if (i == 0 || ids[i] != ids[count - 1])
                {
                    ids[count++] = ids[i];
                }
            }

            Array.Resize(ref ids, count);
            _ids = ids;
            var hash = 17;
            foreach (var id in ids)
            {
                hash = unchecked(hash * 31 + id.GetHashCode());
            }

            _hash = hash;
        }

        public bool Equals(GroupSet other)
        {
            if (_hash != other._hash || _ids.Length != other._ids.Length)
            {
                return false;
            }

            for (var i = 0; i < _ids.Length; i++)
            {
                if (_ids[i] != other._ids[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is GroupSet other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hash;
        }
    }

    private sealed record Stamped<T>(DocumentVersion? Version, T Value);

    private sealed record CatalogEntry(
        IReadOnlyList<ParameterInfo> Catalog,
        IReadOnlyDictionary<ParameterKey, IReadOnlyCollection<long>> Ids);
}
