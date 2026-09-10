using System.Text.RegularExpressions;
using System.Xml.Linq;
using static DevTools.NUnit.Runtime.NUnitNameSyntax;

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
        if (string.Equals(requestedId, testId, StringComparison.Ordinal)
            || string.Equals(requestedId, fullName, StringComparison.Ordinal)
            || string.Equals(requestedId, parentTestId, StringComparison.Ordinal))
            return true;

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
        if (ContainsAtDepthZero(fullName, ArgOpen))
            return false;
        return LastAtDepthZero(fullName, MemberDot) >= 0;
    }

    private static IEnumerable<XNode> ToNodes(string testId)
    {
        yield return new XElement(TestXml, testId);
        if (!IsDottedLeafWithoutArgs(testId))
            yield break;

        NUnitTestNameParser.SplitParts(testId, out var className, out var methodName);
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
            + Regex.Escape(methodName));

    private static bool IsMatch(string? value, string pattern) =>
        !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, pattern);

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
