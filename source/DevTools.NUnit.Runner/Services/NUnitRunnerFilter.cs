using System.Xml.Linq;

namespace DevTools.NUnit.Runner.Services;

public static class NUnitRunnerFilter
{
    private const string InvalidFilterMessage =
        "Filter must be empty, --name/--test selection, or NUnit framework filter XML (starting with '<').";

    private const string MixedFilterMessage =
        "Specify --name/--test or --filter, not both.";

    private static string? Compose(
        IReadOnlyList<string>? names,
        IReadOnlyList<string>? tests,
        string? xml)
    {
        var methodNames = Clean(names);
        var fullNames = Clean(tests);
        var hasSelection = methodNames.Count > 0 || fullNames.Count > 0;
        var hasXml = !string.IsNullOrWhiteSpace(xml);

        if (hasSelection && hasXml)
            throw new ArgumentException(MixedFilterMessage);

        if (hasXml)
            return Normalize(xml);

        if (!hasSelection)
            return null;

        var nodes = methodNames.Select(name => new XElement("name", name))
            .Concat(fullNames.Select(test => new XElement("test", test)))
            .ToList();
        var inner = nodes.Count == 1 ? nodes[0] : new XElement("or", nodes);
        return new XElement("filter", inner).ToString(SaveOptions.DisableFormatting);
    }

    public static bool TryCompose(
        IReadOnlyList<string>? names,
        IReadOnlyList<string>? tests,
        string? xml,
        out string? composed,
        out string? error)
    {
        try
        {
            composed = Compose(names, tests, xml);
            error = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            composed = null;
            error = ex.Message;
            return false;
        }
    }

    public static string? Normalize(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return null;

        var trimmed = filter.Trim();
        if (!trimmed.StartsWith('<', StringComparison.Ordinal))
            throw new ArgumentException(InvalidFilterMessage, nameof(filter));

        return trimmed;
    }

    public static bool TryNormalize(string? filter, out string? normalized, out string? error)
    {
        try
        {
            normalized = Normalize(filter);
            error = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            normalized = null;
            error = ex.Message;
            return false;
        }
    }

    private static List<string> Clean(IReadOnlyList<string>? values) =>
        (values ?? [])
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.Ordinal)
        .ToList();
}
