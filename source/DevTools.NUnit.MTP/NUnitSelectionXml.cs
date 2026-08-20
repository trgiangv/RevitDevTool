using System.Xml.Linq;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP;

/// <summary>
/// Local NUnit name-filter XML. UID / <c>--filter-uid</c> XML is
/// <see cref="DevTools.NUnit.Runtime.NUnitCollapsedSelection"/>.
/// </summary>
internal static class NUnitSelectionXml
{
    public static string? ToFilterXml(IReadOnlyList<string>? names)
    {
        var cleaned = Clean(names);
        if (cleaned.Count == 0)
            return null;

        var nodes = cleaned
            .Select(name => (XNode)new XElement("name", new XAttribute("re", "1"), name))
            .ToList();
        var inner = nodes.Count == 1 ? nodes[0] : new XElement("or", nodes);
        return new XElement("filter", inner).ToString(SaveOptions.DisableFormatting);
    }

    public static string? ToFilterXml(TestingSelection? selection) =>
        ToFilterXml(selection?.Names);

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
