namespace DevTools.ModelsContext.Parameters;

public static class RawValueNames
{
    public static bool TryFind(IReadOnlyDictionary<string, long[]> names, string text, out long[] raws)
    {
        var wanted = text.Trim();
        if (names.TryGetValue(wanted, out var exact))
        {
            raws = exact;
            return true;
        }

        var matches = names.Where(pair => string.Equals(pair.Key.Trim(), wanted, StringComparison.Ordinal))
            .SelectMany(pair => pair.Value)
            .Distinct()
            .ToArray();
        raws = matches;
        return matches.Length > 0;
    }
}
