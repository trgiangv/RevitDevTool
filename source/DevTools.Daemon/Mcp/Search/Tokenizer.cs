using System.Text;

namespace DevTools.Daemon.Mcp.Search;

internal static class Tokenizer
{
    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var normalized = Normalize(text);
        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    internal static string Normalize(string value)
    {
        var withSpaces = SplitCamelCase(value.Replace('_', ' ').Replace('-', ' '));
        return string.Join(' ',
            withSpaces.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.ToLowerInvariant()));
    }

    private static string SplitCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var builder = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (i > 0 && char.IsUpper(c) && (char.IsLower(value[i - 1]) || (i + 1 < value.Length && char.IsLower(value[i + 1]))))
                builder.Append(' ');
            builder.Append(c);
        }

        return builder.ToString();
    }

    public static IReadOnlyList<string> Distinct(IEnumerable<string> tokens)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var distinct = new List<string>();
        foreach (var token in tokens)
        {
            if (seen.Add(token))
                distinct.Add(token);
        }

        return distinct;
    }
}
