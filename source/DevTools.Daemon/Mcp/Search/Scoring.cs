using DevTools.Daemon.Mcp.Processes;

namespace DevTools.Daemon.Mcp.Search;

public static class Scoring
{
    public static double Score(IReadOnlyList<string> queryTokens, CatalogItem item)
    {
        if (queryTokens.Count == 0)
            return 0;

        var target = Tokenizer.Normalize(item.Target);
        var name = Tokenizer.Normalize(item.Resource?.Name ?? item.ResourceTemplate?.Name ?? string.Empty);
        var description = Tokenizer.Normalize(item.Description ?? string.Empty);

        var sum = 0.0;
        var matched = 0;
        foreach (var token in queryTokens)
        {
            var points = 0.0;
            if (target.Contains(token, StringComparison.Ordinal))
                points += 4;
            if (!string.IsNullOrEmpty(name) && name.Contains(token, StringComparison.Ordinal))
                points += 2;
            if (!string.IsNullOrEmpty(description) && description.Contains(token, StringComparison.Ordinal))
                points += 1;
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
}
