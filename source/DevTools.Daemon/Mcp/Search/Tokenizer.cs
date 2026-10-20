using System.Text;
// ReSharper disable UseCollectionExpression

namespace DevTools.Daemon.Mcp.Search;

internal static class Tokenizer
{
    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var separated = SeparateCamelCase(text.Replace('_', ' ').Replace('-', ' '));
        var tokens = new List<string>();
        var token = new StringBuilder();
        foreach (var character in separated)
        {
            if (char.IsLetterOrDigit(character))
            {
                token.Append(char.ToLowerInvariant(character));
                continue;
            }

            AddToken(tokens, token);
        }

        AddToken(tokens, token);
        return tokens;
    }

    private static string SeparateCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var builder = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (IsCamelCaseBoundary(value, i))
                builder.Append(' ');
            builder.Append(c);
        }

        return builder.ToString();
    }

    private static bool IsCamelCaseBoundary(string value, int index) =>
        index > 0
        && char.IsUpper(value[index])
        && (char.IsLower(value[index - 1]) || index + 1 < value.Length && char.IsLower(value[index + 1]));

    private static void AddToken(List<string> tokens, StringBuilder token)
    {
        if (token.Length == 0)
            return;

        tokens.Add(token.ToString());
        token.Clear();
    }

    public static IReadOnlyList<string> Distinct(IEnumerable<string> tokens)
    {
        return tokens.Distinct(StringComparer.Ordinal).ToList();
    }
}
