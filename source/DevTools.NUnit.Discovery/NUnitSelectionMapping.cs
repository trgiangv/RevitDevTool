using System.Xml.Linq;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.Discovery;

public static class NUnitSelectionMapping
{
    public static TestingSelection ToSelection(NUnitDiscoveryFilter filter)
    {
        if (filter.FullNames.Count > 0 && filter.Names.Count == 0)
            return new TestingSelection(filter.FullNames, null);

        if (filter.IsEmpty)
            return new TestingSelection([], null);

        var nodes = filter.Names.Select(name => new XElement("name", name))
            .Concat(filter.FullNames.Select(test => new XElement("test", test)))
            .ToList();
        var inner = nodes.Count == 1 ? nodes[0] : new XElement("or", nodes);
        return new TestingSelection([], new XElement("filter", inner).ToString(SaveOptions.DisableFormatting));
    }

    public static NUnitDiscoveryFilter ToDiscoveryFilter(TestingSelection selection)
    {
        if (selection.TestIds.Count > 0)
            return NUnitDiscoveryFilter.FromFullNames(selection.TestIds);

        var payload = selection.ProviderPayload;
        if (string.IsNullOrWhiteSpace(payload))
            return NUnitDiscoveryFilter.Empty;

        var xml = XElement.Parse(payload);
        var names = xml.Descendants("name")
            .Select(element => element.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();
        var tests = xml.Descendants("test")
            .Select(element => element.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();
        return new NUnitDiscoveryFilter(names, tests);
    }

}
