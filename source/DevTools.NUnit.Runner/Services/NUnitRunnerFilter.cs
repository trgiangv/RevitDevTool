namespace DevTools.NUnit.Runner.Services;

public static class NUnitRunnerFilter
{
    public const string InvalidFilterMessage =
        "Filter must be empty or NUnit framework filter XML consumed by the native runtime (starting with '<'). " +
        "Plain-text NUnit-console where clauses are not supported.";

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
}
