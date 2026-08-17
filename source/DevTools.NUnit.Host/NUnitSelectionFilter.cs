using System.Xml.Linq;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.Host;

internal static class NUnitSelectionFilter
{
    internal const string MixedSelectionMessage =
        "Specify TestIds or ProviderPayload, not both.";

    internal const string InvalidPayloadMessage =
        "ProviderPayload must be empty or NUnit framework filter XML (starting with '<').";

    public static string? ToNUnitFilter(TestingSelection? selection)
    {
        if (selection is null)
            return null;

        var testIds = Clean(selection.TestIds);
        var payload = selection.ProviderPayload?.Trim();
        var hasIds = testIds.Count > 0;
        var hasPayload = !string.IsNullOrWhiteSpace(payload);

        if (hasIds && hasPayload)
            throw new ArgumentException(MixedSelectionMessage, nameof(selection));

        if (hasPayload)
        {
            if (!payload!.StartsWith("<", StringComparison.Ordinal))
                throw new ArgumentException(InvalidPayloadMessage, nameof(selection));

            return payload;
        }

        if (!hasIds)
            return null;

        var nodes = testIds.Select(id => new XElement("test", id)).ToList();
        var inner = nodes.Count == 1 ? nodes[0] : new XElement("or", nodes);
        return new XElement("filter", inner).ToString(SaveOptions.DisableFormatting);
    }

    private static List<string> Clean(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
            return new List<string>();

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
