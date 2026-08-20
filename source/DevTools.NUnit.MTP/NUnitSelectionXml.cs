using System.Xml.Linq;
using DevTools.NUnit.Runtime;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP;

/// <summary>
/// Local NUnit filter XML. <c>--filter</c> → <c>Names</c> → <c>&lt;name&gt;</c>.
/// UID / <c>--filter-uid</c> → collapsed <c>&lt;test&gt;</c> so a Test Explorer
/// method identity (<c>Class.Method</c>) still selects <c>TestName</c> /
/// <c>SetName</c> leaves.
/// </summary>
internal static class NUnitSelectionXml
{
    public static string? ToFilterXml(TestingSelection? selection)
    {
        if (selection is null)
            return null;

        var payload = selection.ProviderPayload?.Trim();
        if (!string.IsNullOrWhiteSpace(payload))
            return payload!.StartsWith("<", StringComparison.Ordinal) ? payload : null;

        var testIds = Clean(selection.TestIds);
        var names = Clean(selection.Names);
        if (testIds.Count == 0 && names.Count == 0)
            return null;

        var nodes = names.Select(name => (XNode)new XElement("name", name))
            .Concat(NUnitCollapsedSelection.ToTestIdNodes(testIds))
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
