using System.Text.RegularExpressions;

namespace Build.Products;

public static class PathGlob
{
    public static bool IsMatch(string pattern, string path)
    {
        var regex = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", "\u0001", StringComparison.Ordinal)
            .Replace(@"\*", "[^/]*", StringComparison.Ordinal)
            .Replace("\u0001", ".*", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(path, regex);
    }

    public static bool IsMatchAny(IEnumerable<string> patterns, string path) => patterns.Any(pattern => IsMatch(pattern, path));
}
