using System.Text.RegularExpressions;

namespace DevTools.NUnit.TestAdapter;

internal static class TestNameParser
{
    private static readonly Regex SplitDotsIgnoreParentheses = new(
        @"(?<!\([^\)]*)\.(?![^\(]*\))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void Split(string testName, out string fullyQualifiedName, out string displayName)
    {
        var parts = SplitDotsIgnoreParentheses.Split(testName);
        var displayIndex = Math.Max(parts.Length - 1, 0);
        fullyQualifiedName = string.Join(".", parts.Take(displayIndex));
        displayName = string.Join(".", parts.Skip(displayIndex));
    }
}
