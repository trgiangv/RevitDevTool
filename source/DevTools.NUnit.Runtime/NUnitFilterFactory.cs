using NUnit.Framework.Internal;

namespace DevTools.NUnit.Runtime;

internal static class NUnitFilterFactory
{
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
