using System.Text.Json;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Mcp.Search;

/// <summary>In-memory token index rebuilt when one process catalog changes.</summary>
internal sealed class SearchIndex
{
    private readonly Dictionary<string, CatalogItem[]> _byToken = new(StringComparer.Ordinal);

    public void Rebuild(IEnumerable<CatalogItem> items)
    {
        _byToken.Clear();
        var tokenMap = new Dictionary<string, List<CatalogItem>>(StringComparer.Ordinal);

        foreach (var item in items)
            AddItemTokens(item, tokenMap);

        foreach (var pair in tokenMap)
            _byToken[pair.Key] = pair.Value.ToArray();
    }

    private static void AddItemTokens(CatalogItem item, Dictionary<string, List<CatalogItem>> tokenMap)
    {
        foreach (var token in TokensFor(item))
        {
            if (!tokenMap.TryGetValue(token, out var list))
            {
                list = [];
                tokenMap[token] = list;
            }

            if (!list.Contains(item))
                list.Add(item);
        }
    }

    public IReadOnlyList<Match> Search(string query, int? processId, IReadOnlyCollection<CatalogType>? kinds, int limit)
    {
        var tokens = Tokenizer.Tokenize(query);
        if (tokens.Count == 0)
            return [];

        var kindFilter = kinds is { Count: > 0 } ? kinds : null;
        var candidates = CollectCandidates(tokens, processId, kindFilter);

        return candidates
            .Select(item => new Match(item, item.ProcessId, Scoring.Score(tokens, item)))
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Item.Kind)
            .ThenBy(match => match.Item.Target, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.Item.ProcessId)
            .Take(limit)
            .ToArray();
    }

    private HashSet<CatalogItem> CollectCandidates(
        IReadOnlyList<string> tokens,
        int? processId,
        IReadOnlyCollection<CatalogType>? kindFilter)
    {
        var candidates = new HashSet<CatalogItem>();
        foreach (var token in tokens)
        {
            if (!_byToken.TryGetValue(token, out var bucket))
                continue;

            foreach (var item in bucket)
            {
                if (MatchesFilter(item, processId, kindFilter))
                    candidates.Add(item);
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

    private static IEnumerable<string> TokensFor(CatalogItem item)
    {
        foreach (var token in Tokenizer.Tokenize(item.Target))
            yield return token;
        foreach (var token in Tokenizer.Tokenize(item.Resource?.Name ?? item.ResourceTemplate?.Name))
            yield return token;
        foreach (var token in Tokenizer.Tokenize(item.Description))
            yield return token;
        foreach (var token in ToolSchemaPropertyNameTokens(item.Tool))
            yield return token;
    }

    private static IEnumerable<string> ToolSchemaPropertyNameTokens(Tool? tool)
    {
        if (tool is null)
            yield break;

        var schema = tool.InputSchema;
        if (schema.ValueKind != JsonValueKind.Object)
            yield break;
        if (!schema.TryGetProperty(McpSpecKeys.JsonSchema.Properties, out var properties))
            yield break;
        if (properties.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var property in properties.EnumerateObject())
        foreach (var token in Tokenizer.Tokenize(property.Name))
            yield return token;
    }
}

public sealed record Match(CatalogItem Item, int ProcessId, double Score);
