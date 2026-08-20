using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DevTools.NUnit.Runtime;

/// <summary>
/// Testhost ExploreTests may emit a NotRunnable stub <c>Class.Method</c>
/// while in-host source expansion uses <c>Class("args").Method</c> or
/// <c>SetName</c> leaves under the original method suite. UID runs keep the
/// stub identity; the host filter also matches those expansions.
/// IDs that already contain a depth-0 <c>(</c> stay exact <c>&lt;test&gt;</c>.
/// </summary>
internal static class NUnitCollapsedSelection
{
    public static string? ToFilterXml(IReadOnlyList<string>? testIds)
    {
        var nodes = ToTestIdNodes(testIds);
        if (nodes.Count == 0)
            return null;

        var inner = nodes.Count == 1 ? nodes[0] : new XElement("or", nodes);
        return new XElement("filter", inner).ToString(SaveOptions.DisableFormatting);
    }

    public static List<XNode> ToTestIdNodes(IReadOnlyList<string>? testIds) =>
        Clean(testIds).SelectMany(ToNodes).ToList();

    public static bool Matches(
        string requestedId,
        string? testId,
        string? fullName,
        string? parentTestId)
    {
        if (string.IsNullOrWhiteSpace(requestedId))
            return false;

        requestedId = requestedId.Trim();
        if (string.Equals(requestedId, testId, StringComparison.Ordinal)
            || string.Equals(requestedId, fullName, StringComparison.Ordinal)
            || string.Equals(requestedId, parentTestId, StringComparison.Ordinal))
            return true;

        if (!IsDottedLeafWithoutArgs(requestedId))
            return false;

        SplitClassMethod(requestedId, out var className, out var methodName);
        var pattern = ExpandedFullNamePattern(className, methodName);
        return IsMatch(testId, pattern) || IsMatch(fullName, pattern);
    }

    internal static bool IsDottedLeafWithoutArgs(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return false;
        if (HasDepthZeroChar(fullName, '('))
            return false;
        return LastDotAtDepthZero(fullName) >= 0;
    }

    private static IEnumerable<XNode> ToNodes(string testId)
    {
        yield return new XElement("test", testId);
        if (!IsDottedLeafWithoutArgs(testId))
            yield break;

        SplitClassMethod(testId, out var className, out var methodName);
        yield return new XElement(
            "test",
            new XAttribute("re", "1"),
            ExpandedFullNamePattern(className, methodName));
        yield return new XElement(
            "and",
            new XElement(
                "class",
                new XAttribute("re", "1"),
                "^" + Regex.Escape(className) + @"(\([^)]*\))?$"),
            new XElement("method", methodName));
    }

    private static string ExpandedFullNamePattern(string className, string methodName) =>
        "^" + Regex.Escape(className) + @"(\([^)]*\))?\." + Regex.Escape(methodName) + "$";

    private static bool IsMatch(string? value, string pattern) =>
        !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, pattern);

    private static void SplitClassMethod(string fullName, out string className, out string methodName)
    {
        var lastDot = LastDotAtDepthZero(fullName);
        if (lastDot < 0)
        {
            className = fullName;
            methodName = fullName;
            return;
        }

        className = fullName.Substring(0, lastDot);
        methodName = fullName.Substring(lastDot + 1);
    }

    private static bool HasDepthZeroChar(string value, char symbol)
    {
        var depth = 0;
        foreach (var c in value)
        {
            switch (c)
            {
                case '(':
                case '<':
                    if (c == symbol && depth == 0)
                        return true;
                    depth++;
                    break;
                case ')':
                case '>':
                    if (depth > 0)
                        depth--;
                    break;
                default:
                    if (c == symbol && depth == 0)
                        return true;
                    break;
            }
        }

        return false;
    }

    private static int LastDotAtDepthZero(string value)
    {
        var depth = 0;
        var last = -1;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '(':
                case '<':
                    depth++;
                    break;
                case ')':
                case '>':
                    if (depth > 0)
                        depth--;
                    break;
                case '.' when depth == 0:
                    last = index;
                    break;
            }
        }

        return last;
    }

    private static List<string> Clean(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
            return [];

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
