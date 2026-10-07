using DevTools.Daemon.Mcp.Processes;

namespace DevTools.Daemon.Mcp.Search;

/// <summary>In-memory token index rebuilt when one process catalog changes.</summary>
internal sealed class SearchIndex
{
    private readonly Dictionary<string, int[]> _byToken = new(StringComparer.Ordinal);
    private CatalogItem[] _items = [];
    private Scoring.FieldTokens[] _fields = [];
    private ScoringCorpus _corpus = ScoringCorpus.Empty;

    public void Rebuild(IEnumerable<CatalogItem> items)
    {
        var itemList = items as IReadOnlyList<CatalogItem> ?? items.ToArray();
        _items = itemList as CatalogItem[] ?? itemList.ToArray();
        _fields = new Scoring.FieldTokens[_items.Length];
        for (var index = 0; index < _items.Length; index++)
            _fields[index] = Scoring.Fields(_items[index]);

        _corpus = Scoring.Build(_fields);
        _byToken.Clear();
        var tokenMap = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var index = 0; index < _fields.Length; index++)
        {
            foreach (var token in Tokenizer.Distinct(_fields[index].All))
            {
                if (!tokenMap.TryGetValue(token, out var list))
                {
                    list = [];
                    tokenMap[token] = list;
                }

                list.Add(index);
            }
        }

        foreach (var pair in tokenMap)
            _byToken[pair.Key] = pair.Value.ToArray();
    }

    public IReadOnlyList<Match> Search(string query, int? processId, IReadOnlyCollection<CatalogType>? kinds, int limit)
    {
        var tokens = Tokenizer.Distinct(Tokenizer.Tokenize(query));
        if (tokens.Count == 0)
            return [];

        var kindFilter = kinds is { Count: > 0 } ? kinds : null;
        return CollectCandidates(tokens, processId, kindFilter)
            .Select(index => (index, score: Scoring.Score(tokens, _fields[index], _corpus)))
            .Where(candidate => candidate.score > 0)
            .OrderByDescending(candidate => candidate.score)
            .ThenBy(candidate => _items[candidate.index].Kind)
            .ThenBy(candidate => _items[candidate.index].Target, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => _items[candidate.index].ProcessId)
            .Take(limit)
            .Select(candidate => new Match(_items[candidate.index], _items[candidate.index].ProcessId))
            .ToArray();
    }

    private HashSet<int> CollectCandidates(
        IReadOnlyList<string> tokens,
        int? processId,
        IReadOnlyCollection<CatalogType>? kindFilter)
    {
        var candidates = new HashSet<int>();
        foreach (var token in tokens)
        {
            if (!_byToken.TryGetValue(token, out var bucket))
                continue;

            foreach (var index in bucket)
            {
                if (MatchesFilter(_items[index], processId, kindFilter))
                    candidates.Add(index);
            }
        }

        return candidates;
    }

    private static bool MatchesFilter(
        CatalogItem item,
        int? processId,
        IReadOnlyCollection<CatalogType>? kindFilter)
    {
        if (processId is not null && item.ProcessId != processId)
            return false;
        return kindFilter is null || kindFilter.Contains(item.Kind);
    }
}

public sealed record Match(CatalogItem Item, int ProcessId);
