using NUnit.Framework.Internal;

namespace DevTools.NUnit.Runtime;

internal static class NUnitFilterFactory
{
    /// <summary>
    /// <see cref="DevTools.Testing.Abstractions.Contracts.TestSelection.FilterFormat"/> for NUnit
    /// <c>TestFilter.FromXml</c>. The only <c>FrameworkFilter</c> format the in-host NUnit runtime accepts.
    /// </summary>
    public const string XmlFilterFormat = "filter-xml";

    /// <summary>Filter that matches no test. An empty selection runs nothing, never everything.</summary>
    public const string RunNothingXml = "<filter><not><test>*</test></not></filter>";

    private const string UnsupportedFilterMessage =
        "Filter must be empty or NUnit framework filter XML consumed by TestFilter.FromXml.";

    public static TestFilter Create(string? filterExpression)
    {
        if (string.IsNullOrWhiteSpace(filterExpression))
            return TestFilter.Empty;

        var xml = filterExpression!.Trim();
        try
        {
            return TestFilter.FromXml(xml);
        }
        catch (Exception ex) when (ex is not ArgumentException { ParamName: nameof(filterExpression) })
        {
            throw new ArgumentException(UnsupportedFilterMessage, nameof(filterExpression), ex);
        }
    }
}
