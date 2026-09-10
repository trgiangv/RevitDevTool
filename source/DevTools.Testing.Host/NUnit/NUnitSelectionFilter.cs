using System.Xml;
using System.Xml.Linq;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Host.NUnit;

internal static class NUnitSelectionFilter
{
    /// <summary>
    /// <see cref="TestSelection.FilterFormat"/> for NUnit
    /// <c>TestFilter.FromXml</c> (NUnit 4.6.1). Runtime consumes this as
    /// <see cref="TestSelectionKind.FrameworkFilter"/>.
    /// </summary>
    public const string XmlFilterFormat = "filter-xml";

    // Element / attribute names from NUnit.Framework.Internal.TestFilter.FromXml.
    private const string FilterXml = "filter";
    private const string TestXml = "test";
    private const string NameXml = "name";
    private const string OrXml = "or";
    private const string NotXml = "not";
    private const string RegexXml = "re";
    private const string RegexEnabled = "1";
    private const string MatchAll = "*";

    internal const string InvalidPayloadMessage =
        "FrameworkFilter Data must be NUnit filter XML with a <" + FilterXml + "> root.";

    /// <summary>
    /// Maps a closed <see cref="TestSelection"/> onto NUnit
    /// <c>TestFilter.FromXml</c> text, or <see langword="null"/> for
    /// unconstrained (<see cref="TestSelectionKind.All"/>).
    /// <list type="bullet">
    /// <item><see cref="TestSelectionKind.FrameworkFilter"/> — <see cref="TestSelection.FilterData"/> already XML; format must be <see cref="XmlFilterFormat"/> and the root element <c>filter</c>.</item>
    /// <item><see cref="TestSelectionKind.TestIds"/> — each id is a <c>test</c> (FullName) node. Empty list is run-nothing: <c>not/test *</c>, not run-all.</item>
    /// <item><see cref="TestSelectionKind.Names"/> — each name is a <c>name</c> node with <c>re="1"</c> (NUnit TestNameFilter regex).</item>
    /// </list>
    /// In-host execution only accepts All or this XML; the provider maps TestIds/Names here before <c>testing/run</c>.
    /// </summary>
    public static string? ToNUnitFilter(TestSelection? selection) => selection?.Kind switch
    {
        null or TestSelectionKind.All => null,
        TestSelectionKind.FrameworkFilter => RequireFilterXml(selection),
        TestSelectionKind.TestIds => FromTerms(selection.TestIds, id => new XElement(TestXml, id)),
        TestSelectionKind.Names => FromTerms(selection.Names, name =>
            new XElement(NameXml, new XAttribute(RegexXml, RegexEnabled), name)),
        _ => throw new ArgumentOutOfRangeException(nameof(selection)),
    };

    private static string RequireFilterXml(TestSelection selection)
    {
        if (!string.Equals(selection.FilterFormat, XmlFilterFormat, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"FrameworkFilter format must be '{XmlFilterFormat}'.",
                nameof(selection));
        }

        try
        {
            var root = XElement.Parse(selection.FilterData!);
            if (string.Equals(root.Name.LocalName, FilterXml, StringComparison.Ordinal))
                return selection.FilterData!.Trim();
        }
        catch (XmlException)
        {
        }

        throw new ArgumentException(InvalidPayloadMessage, nameof(selection));
    }

    private static string FromTerms(IReadOnlyList<string>? values, Func<string, XElement> node)
    {
        var nodes = (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Select(node)
            .ToList();
        var inner = nodes.Count switch
        {
            0 => new XElement(NotXml, new XElement(TestXml, MatchAll)),
            1 => nodes[0],
            _ => new XElement(OrXml, nodes),
        };
        return new XElement(FilterXml, inner).ToString(SaveOptions.DisableFormatting);
    }
}
