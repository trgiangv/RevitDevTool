using DevTools.Daemon.Mcp.Processes;
using DevTools.Mcp.Core.Protocol;
// ReSharper disable UseCollectionExpression

namespace DevTools.Daemon.Mcp.Search;

/// <summary>Separates specialized discovery from the generic code-execution escape hatch.</summary>
internal static class SearchPolicy
{
    internal const double MinimumCoverage = 0.6;
    private static readonly HashSet<string> ExecutionFallbackNames = new(StringComparer.OrdinalIgnoreCase)
    {
        McpSpecKeys.Tool.ExecuteCSharp,
        McpSpecKeys.Tool.ExecutePython,
    };
    private static readonly HashSet<string> ExecutionIntentTokens = new(StringComparer.Ordinal)
    {
        "execute", "code", "csharp", "python", "script",
    };
    private static readonly HashSet<string> AttributeConstraintMarkers = new(StringComparer.Ordinal)
    {
        "with", "where", "having", "whose",
    };
    // Grammar and request words do not express a capability requirement.
    private static readonly HashSet<string> CoverageIgnoredTokens = new(AttributeConstraintMarkers, StringComparer.Ordinal)
    {
        "show", "display", "please",
    };

    public static bool IsExecutionFallback(CatalogItem item) =>
        item.Kind == CatalogType.Tool && ExecutionFallbackNames.Contains(item.Target);

    public static bool HasExecutionIntent(IReadOnlyList<string> tokens) =>
        tokens.Any(ExecutionIntentTokens.Contains);

    public static double QueryCoverage(
        IReadOnlyList<string> tokens,
        Scoring.FieldTokens fields,
        ScoringCorpus corpus)
    {
        var (total, matched) = ExactCoverage(tokens, fields, corpus);
        matched += AttributeConstraintCoverage(tokens, fields, corpus);
        return total > 0 ? matched / total : 0;
    }

    private static (double Total, double Matched) ExactCoverage(
        IReadOnlyList<string> tokens,
        Scoring.FieldTokens fields,
        ScoringCorpus corpus)
    {
        var total = 0.0;
        var matched = 0.0;
        foreach (var token in tokens)
        {
            if (CoverageIgnoredTokens.Contains(token))
                continue;

            var weight = Scoring.Idf(token, corpus);
            total += weight;
            if (fields.Frequencies.ContainsKey(token))
                matched += weight;
        }

        return (total, matched);
    }

    private static double AttributeConstraintCoverage(
        IReadOnlyList<string> tokens,
        Scoring.FieldTokens fields,
        ScoringCorpus corpus)
    {
        if (!fields.SupportsArbitraryParameterFilter
            || !TryFindAttributeConstraint(tokens, fields, out var constraintStart))
            return 0;

        return ConstraintSuffixCoverage(tokens, constraintStart, fields, corpus);
    }

    private static double ConstraintSuffixCoverage(
        IReadOnlyList<string> tokens,
        int constraintStart,
        Scoring.FieldTokens fields,
        ScoringCorpus corpus)
    {
        var matched = 0.0;
        for (var index = constraintStart; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (CoverageIgnoredTokens.Contains(token) || fields.Frequencies.ContainsKey(token))
                continue;

            matched += Scoring.Idf(token, corpus);
        }

        return matched;
    }

    private static bool TryFindAttributeConstraint(
        IReadOnlyList<string> tokens,
        Scoring.FieldTokens fields,
        out int constraintStart)
    {
        for (var markerIndex = 1; markerIndex < tokens.Count - 1; markerIndex++)
        {
            if (!AttributeConstraintMarkers.Contains(tokens[markerIndex])
                || !HasMatchedSubject(tokens, markerIndex, fields))
                continue;

            constraintStart = markerIndex + 1;
            return true;
        }

        constraintStart = 0;
        return false;
    }

    private static bool HasMatchedSubject(
        IReadOnlyList<string> tokens,
        int endIndex,
        Scoring.FieldTokens fields)
    {
        for (var index = 0; index < endIndex; index++)
            if (fields.Frequencies.ContainsKey(tokens[index]))
                return true;

        return false;
    }

    public static IReadOnlyList<Match> ExecutionFallbacks(
        IEnumerable<CatalogItem> items,
        int? processId,
        IReadOnlyCollection<CatalogType>? kinds,
        int limit)
    {
        if (kinds is { Count: > 0 } && !kinds.Contains(CatalogType.Tool))
            return [];

        return items
            .Where(item => IsExecutionFallback(item) && (processId is null || item.ProcessId == processId))
            .OrderBy(item => item.Target, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProcessId)
            .Take(limit)
            .Select(item => new Match(item, item.ProcessId))
            .ToArray();
    }
}
