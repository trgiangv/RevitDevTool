using DevTools.Daemon.Mcp.Processes;
using DevTools.Mcp.Core.Protocol;

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

        foreach (var pair in tokenMap)
            _byToken[pair.Key] = pair.Value.ToArray();
    }

    public IReadOnlyList<Match> Search(string query, int? processId, IReadOnlyCollection<CatalogType>? kinds, int limit)
    {
        var tokens = Tokenizer.Tokenize(query);
        if (tokens.Count == 0)
            return [];

        var kindFilter = kinds is { Count: > 0 } ? kinds : null;
        var candidates = new HashSet<CatalogItem>();

        foreach (var token in tokens)
        {
            if (!_byToken.TryGetValue(token, out var bucket))
                continue;

            foreach (var item in bucket)
            {
                if (processId is not null && item.ProcessId != processId)
                    continue;
                if (kindFilter is not null && !kindFilter.Contains(item.Kind))
                    continue;
                candidates.Add(item);
            }
        }

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

    private static IEnumerable<string> TokensFor(CatalogItem item)
    {
        foreach (var token in Tokenizer.Tokenize(item.Target))
            yield return token;
        foreach (var token in Tokenizer.Tokenize(item.Resource?.Name ?? item.ResourceTemplate?.Name))
            yield return token;
        foreach (var token in Tokenizer.Tokenize(item.Description))
            yield return token;

        if (item.Tool?.InputSchema.ValueKind == System.Text.Json.JsonValueKind.Object &&
            item.Tool.InputSchema.TryGetProperty(McpSpecKeys.JsonSchema.Properties, out var properties) &&
            properties.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            foreach (var token in Tokenizer.Tokenize(property.Name))
                yield return token;
        }
    }
}

public sealed record Match(CatalogItem Item, int ProcessId, double Score);
