using DevTools.Daemon.Mcp.Processes;
// ReSharper disable UseCollectionExpression
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace DevTools.Daemon.Mcp.Search;

/// <summary>In-memory token index rebuilt when one process catalog changes.</summary>
internal sealed class SearchIndex
{
    private static readonly IComparer<ScoredMatch> HighestFirst = Comparer<ScoredMatch>.Create(CompareByRank);
    private static readonly IComparer<ScoredMatch> LowestFirst = Comparer<ScoredMatch>.Create(
        (left, right) => CompareByRank(right, left));

    private readonly Dictionary<string, int[]> _byToken = new(StringComparer.Ordinal);
    private CatalogItem[] _items = [];
    private Scoring.FieldTokens[] _fields = [];
    private ScoringCorpus _corpus = ScoringCorpus.Empty;

    public void Rebuild(IEnumerable<CatalogItem> items)
    {
        _items = items.ToArray();
        _fields = _items.Select(Scoring.Fields).ToArray();
        _corpus = Scoring.Build(_fields);
        RebuildTokenIndex();
    }

    private void RebuildTokenIndex()
    {
        _byToken.Clear();
        var tokenMap = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var index = 0; index < _fields.Length; index++)
            AddDocumentTokens(tokenMap, index);

        foreach (var pair in tokenMap)
            _byToken[pair.Key] = pair.Value.ToArray();
    }

    private void AddDocumentTokens(Dictionary<string, List<int>> tokenMap, int index)
    {
        foreach (var token in _fields[index].All)
        {
            if (!tokenMap.TryGetValue(token, out var documents))
                tokenMap[token] = documents = [];

            documents.Add(index);
        }
    }

    public IReadOnlyList<Match> Search(string query, int? processId, IReadOnlyCollection<CatalogType>? kinds, int limit)
    {
        var tokens = Tokenizer.Distinct(Tokenizer.Tokenize(query));
        if (tokens.Count == 0 || limit <= 0)
            return [];

        var kindFilter = kinds is { Count: > 0 } ? kinds : null;
        var executionIntent = SearchPolicy.HasExecutionIntent(tokens);
        var topMatches = new PriorityQueue<ScoredMatch, ScoredMatch>(LowestFirst);
        foreach (var index in CollectCandidates(tokens, processId, kindFilter))
        {
            var item = _items[index];
            if (!executionIntent && SearchPolicy.IsExecutionFallback(item))
                continue;
            var queryCoverage = SearchPolicy.QueryCoverage(tokens, _fields[index], _corpus);
            if (queryCoverage < SearchPolicy.MinimumCoverage)
                continue;
            AddScoredMatch(index, tokens, queryCoverage, limit, topMatches);
        }

        return topMatches.Count > 0 ? ToMatches(topMatches) : SearchPolicy.ExecutionFallbacks(_items, processId, kindFilter, limit);
    }

    private static Match[] ToMatches(PriorityQueue<ScoredMatch, ScoredMatch> topMatches) =>
        topMatches.UnorderedItems
            .Select(entry => entry.Element)
            .OrderBy(match => match, HighestFirst)
            .Select(match => new Match(match.Item, match.Item.ProcessId))
            .ToArray();

    private void AddScoredMatch(
        int index,
        IReadOnlyList<string> tokens,
        double queryCoverage,
        int limit,
        PriorityQueue<ScoredMatch, ScoredMatch> topMatches)
    {
        var item = _items[index];
        var score = Scoring.Score(tokens, _fields[index], _corpus);
        if (score <= 0)
            return;

        var match = new ScoredMatch(index, item, score, queryCoverage);
        if (topMatches.Count < limit)
        {
            topMatches.Enqueue(match, match);
            return;
        }

        if (CompareByRank(match, topMatches.Peek()) >= 0) return;
        topMatches.Dequeue();
        topMatches.Enqueue(match, match);
    }

    private static int CompareByRank(ScoredMatch left, ScoredMatch right)
    {
        var comparison = right.QueryCoverage.CompareTo(left.QueryCoverage);
        if (comparison != 0)
            return comparison;

        comparison = right.Score.CompareTo(left.Score);
        if (comparison != 0)
            return comparison;

        comparison = left.Item.Kind.CompareTo(right.Item.Kind);
        if (comparison != 0)
            return comparison;

        comparison = StringComparer.OrdinalIgnoreCase.Compare(left.Item.Target, right.Item.Target);
        if (comparison != 0)
            return comparison;

        comparison = left.Item.ProcessId.CompareTo(right.Item.ProcessId);
        return comparison != 0 ? comparison : left.Index.CompareTo(right.Index);
    }

    private HashSet<int> CollectCandidates(
        IReadOnlyList<string> tokens,
        int? processId,
        IReadOnlyCollection<CatalogType>? kindFilter)
    {
        var candidates = new HashSet<int>();
        foreach (var token in tokens)
            AddCandidates(token, processId, kindFilter, candidates);

        return candidates;
    }

    private void AddCandidates(
        string token,
        int? processId,
        IReadOnlyCollection<CatalogType>? kindFilter,
        HashSet<int> candidates)
    {
        if (!_byToken.TryGetValue(token, out var bucket))
            return;

        foreach (var index in bucket)
        {
            if (MatchesFilter(_items[index], processId, kindFilter))
                candidates.Add(index);
        }
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

    private readonly record struct ScoredMatch(int Index, CatalogItem Item, double Score, double QueryCoverage);
}

public sealed record Match(CatalogItem Item, int ProcessId);
