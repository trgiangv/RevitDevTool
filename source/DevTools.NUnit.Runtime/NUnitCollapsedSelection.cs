using System.Text.RegularExpressions;
using System.Xml.Linq;
using static DevTools.NUnit.Runtime.NUnitNameSyntax;

namespace DevTools.NUnit.Runtime;

/// <summary>
/// Testhost ExploreTests may emit a NotRunnable stub <c>Class.Method</c>
/// while in-host source expansion uses <c>Class("args").Method</c> or
/// <c>SetName</c> leaves under the original method suite. UID runs keep the
/// stub identity; the host filter also matches those expansions.
/// Discovery strips NUnit 4 numeric suffixes from the TestNode uid
/// (<c>12.3d</c> → <c>12.3</c>); the host filter still matches in-host
/// FullName that kept <c>d</c>. IDE grouping uids are resolved by
/// <c>NUnitIdentityIndex</c>, not by inbound DisplayName regex.
/// </summary>
internal static class NUnitCollapsedSelection
{
    private const string FilterXml = "filter";
    private const string OrXml = "or";
    private const string TestXml = "test";
    private const string AndXml = "and";
    private const string ClassXml = "class";
    private const string MethodXml = "method";
    private const string RegexXml = "re";
    private const string RegexEnabled = "1";
    private const string OptionalCtorArgs = @"(\([^)]*\))?";

    public static string? ToFilterXml(IReadOnlyList<string>? testIds)
    {
        var nodes = ToTestIdNodes(testIds);
        if (nodes.Count == 0)
            return null;

        var inner = nodes.Count == 1 ? nodes[0] : new XElement(OrXml, nodes);
        return new XElement(FilterXml, inner).ToString(SaveOptions.DisableFormatting);
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
        if (Same(requestedId, testId)
            || Same(requestedId, fullName)
            || Same(requestedId, parentTestId))
            return true;

        requestedId = TrimEmptyParentheses(requestedId);
        if (!IsDottedLeafWithoutArgs(requestedId))
            return false;

        NUnitTestNameParser.SplitParts(requestedId, out var className, out var methodName);
        var pattern = ExpandedFullNamePattern(className, methodName);
        return IsMatch(testId, pattern) || IsMatch(fullName, pattern);
    }

    internal static bool IsDottedLeafWithoutArgs(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return false;
        fullName = TrimEmptyParentheses(fullName);
        if (ContainsAtDepthZero(fullName, ArgOpen))
            return false;
        return LastAtDepthZero(fullName, MemberDot) >= 0;
    }

    private static IEnumerable<XNode> ToNodes(string testId)
    {
        yield return new XElement(TestXml, testId);
        var unsuffixed = StripNumericTypeSuffixes(testId);
        if (!string.Equals(unsuffixed, testId, StringComparison.Ordinal))
            yield return new XElement(TestXml, unsuffixed);

        var canonical = Canonicalize(testId);
        if (!string.Equals(canonical, testId, StringComparison.Ordinal)
            && !string.Equals(canonical, unsuffixed, StringComparison.Ordinal))
            yield return new XElement(TestXml, canonical);

        var grouping = TrimEmptyParentheses(unsuffixed);
        if (!string.Equals(grouping, testId, StringComparison.Ordinal)
            && !string.Equals(grouping, unsuffixed, StringComparison.Ordinal)
            && !string.Equals(grouping, canonical, StringComparison.Ordinal))
            yield return new XElement(TestXml, grouping);

        if (ContainsAtDepthZero(canonical, ArgOpen))
        {
            yield return RegexTest(OptionalNumericSuffixPattern(canonical));
            yield break;
        }

        if (!IsDottedLeafWithoutArgs(grouping))
            yield break;

        NUnitTestNameParser.SplitParts(grouping, out var className, out var methodName);
        yield return RegexTest(ExpandedFullNamePattern(className, methodName));
        yield return new XElement(
            AndXml,
            RegexClass(className),
            new XElement(MethodXml, methodName));
    }

    private static XElement RegexTest(string pattern) =>
        new(TestXml, new XAttribute(RegexXml, RegexEnabled), pattern);

    private static XElement RegexClass(string className) =>
        new(ClassXml, new XAttribute(RegexXml, RegexEnabled),
            Anchored(Regex.Escape(className) + OptionalCtorArgs));

    private static string ExpandedFullNamePattern(string className, string methodName) =>
        Anchored(
            Regex.Escape(className)
            + OptionalCtorArgs
            + Regex.Escape(MemberDot.ToString())
            + Regex.Escape(methodName)
            + OptionalCtorArgs);

    private static bool IsMatch(string? value, string pattern) =>
        !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, pattern);

    private static string OptionalNumericSuffixPattern(string canonical) =>
        Anchored(Regex.Replace(Regex.Escape(canonical), @"(\d+(?:\\\.\d+)?)", "$1[dDfFmML]?"));

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
