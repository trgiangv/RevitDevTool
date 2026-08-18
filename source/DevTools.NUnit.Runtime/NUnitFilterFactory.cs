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

        var trimmed = filterExpression!.Trim();
        if (!trimmed.StartsWith("<", StringComparison.Ordinal))
            throw new ArgumentException(UnsupportedFilterMessage, nameof(filterExpression));

        return TestFilter.FromXml(trimmed);
    }
}
