using DevTools.Daemon.Mcp.Processes;

namespace DevTools.Daemon.Mcp.Search;

public static class Scoring
{
    public static double Score(IReadOnlyList<string> queryTokens, CatalogItem item)
    {
        if (queryTokens.Count == 0)
            return 0;

        var fields = NormalizeFields(item);
        var sum = 0.0;
        var matched = 0;
        foreach (var token in queryTokens)
        {
            var points = ScoringFields(token, fields);
            if (points == 0)
                continue;

            matched++;
            sum += points;
        }

        // Drop when fewer than half the query tokens matched any field.
        if (matched * 2 < queryTokens.Count)
            return 0;

        return sum / queryTokens.Count;
    }

    private static (string Target, string Name, string Description) NormalizeFields(CatalogItem item)
    {
        var name = item.Resource?.Name ?? item.ResourceTemplate?.Name ?? string.Empty;
        return (
            Tokenizer.Normalize(item.Target),
            Tokenizer.Normalize(name),
            Tokenizer.Normalize(item.Description ?? string.Empty));
    }

    private static double ScoringFields(string token, (string Target, string Name, string Description) fields)
    {
        var points = 0.0;
        if (fields.Target.Contains(token, StringComparison.Ordinal))
            points += 4;
        if (fields.Name.Length > 0 && fields.Name.Contains(token, StringComparison.Ordinal))
            points += 2;
        if (fields.Description.Length > 0 && fields.Description.Contains(token, StringComparison.Ordinal))
            points += 1;
        return points;
    }
}
