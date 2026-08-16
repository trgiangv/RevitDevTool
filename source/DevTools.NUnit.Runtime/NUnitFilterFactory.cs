using NUnit.Framework.Internal;

namespace DevTools.NUnit.Runtime;

internal static class NUnitFilterFactory
{
    private const string UnsupportedFilterMessage =
        "Filter must be empty or NUnit framework filter XML consumed by TestFilter.FromXml. " +
        "Runner and MTP must construct filter XML or neutral selected test IDs; " +
        "plain-text NUnit-console where clauses are not supported by the runtime.";

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
