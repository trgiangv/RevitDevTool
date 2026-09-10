using System.Xml.Linq;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.Host;

internal static class NUnitSelectionFilter
{
    internal const string InvalidPayloadMessage =
        "FrameworkFilter Data must be NUnit filter XML (starting with '<').";

    internal const string FilterFormat = TestingSelection.XmlFilterFormat;

    public static string? ToNUnitFilter(TestingSelection? selection)
    {
        if (selection is null || selection.Kind == TestingSelectionKind.All)
            return null;

        if (selection.Kind == TestingSelectionKind.FrameworkFilter)
        {
            if (!string.Equals(selection.FilterFormat, FilterFormat, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"FrameworkFilter format must be '{FilterFormat}'.",
                    nameof(selection));
            }

            var payload = selection.FilterData?.Trim();
            if (string.IsNullOrWhiteSpace(payload) || !payload!.StartsWith("<", StringComparison.Ordinal))
                throw new ArgumentException(InvalidPayloadMessage, nameof(selection));
            return payload;
        }

        var testIds = selection.Kind == TestingSelectionKind.TestIds ? Clean(selection.TestIds) : [];
        var names = selection.Kind == TestingSelectionKind.Names ? Clean(selection.Names) : [];
        if (testIds.Count == 0 && names.Count == 0)
            return new XElement("filter", new XElement("not", new XElement("test", "*"))).ToString(SaveOptions.DisableFormatting);

        var nodes = names.Select(name => new XElement("name", new XAttribute("re", "1"), name))
            .Concat(testIds.Select(id => new XElement("test", id)))
            .ToList();
        var inner = nodes.Count == 1 ? nodes[0] : new XElement("or", nodes);
        return new XElement("filter", inner).ToString(SaveOptions.DisableFormatting);
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
