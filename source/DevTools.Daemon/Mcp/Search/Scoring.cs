using System.Text.Json;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Mcp.Search;

/// <summary>
/// BM25F over catalog fields. Weights: target 4, name 2, description 1, parameter names 1.
/// </summary>
public static class Scoring
{
    public const double K1 = 1.2;
    public const double B = 0.75;

    public static ScoringCorpus Build(IReadOnlyList<CatalogItem> items) =>
        Build(items.Select(Fields).ToArray());

    internal static ScoringCorpus Build(IReadOnlyList<FieldTokens> documents)
    {
        var frequency = new Dictionary<string, int>(StringComparer.Ordinal);
        double target = 0;
        double name = 0;
        double description = 0;
        double parameters = 0;

        foreach (var fields in documents)
        {
            target += fields.Target.Length;
            name += fields.Name.Length;
            description += fields.Description.Length;
            parameters += fields.Parameters.Length;

            foreach (var token in Tokenizer.Distinct(fields.All))
            {
                frequency.TryGetValue(token, out var count);
                frequency[token] = count + 1;
            }
        }

        var n = documents.Count;
        return new ScoringCorpus(
            n,
            frequency,
            Average(target, n),
            Average(name, n),
            Average(description, n),
            Average(parameters, n));
    }

    public static double Score(IReadOnlyList<string> queryTokens, CatalogItem item, ScoringCorpus corpus) =>
        Score(queryTokens, Fields(item), corpus);

    internal static double Score(IReadOnlyList<string> queryTokens, FieldTokens fields, ScoringCorpus corpus)
    {
        if (queryTokens.Count == 0 || corpus.DocumentCount == 0)
            return 0;

        var sum = 0.0;
        foreach (var token in Tokenizer.Distinct(queryTokens))
        {
            var weighted = WeightedTermFrequency(token, fields, corpus);
            if (weighted <= 0)
                continue;

            var idf = Idf(token, corpus);
            sum += idf * (weighted * (K1 + 1)) / (weighted + K1);
        }

        return sum;
    }

    private static double WeightedTermFrequency(string token, FieldTokens fields, ScoringCorpus corpus) =>
        Term(4, Count(fields.Target, token), fields.Target.Length, corpus.AverageTargetLength)
        + Term(2, Count(fields.Name, token), fields.Name.Length, corpus.AverageNameLength)
        + Term(1, Count(fields.Description, token), fields.Description.Length, corpus.AverageDescriptionLength)
        + Term(1, Count(fields.Parameters, token), fields.Parameters.Length, corpus.AverageParameterLength);

    private static double Term(double weight, int tf, int length, double averageLength)
    {
        if (tf == 0)
            return 0;

        var average = averageLength <= 0 ? 1 : averageLength;
        var norm = 1 - B + (B * length / average);
        return weight * tf / norm;
    }

    private static double Idf(string token, ScoringCorpus corpus)
    {
        corpus.DocumentFrequency.TryGetValue(token, out var df);
        var n = corpus.DocumentCount;
        return Math.Log(1 + ((n - df + 0.5) / (df + 0.5)));
    }

    private static int Count(string[] tokens, string token)
    {
        var count = 0;
        foreach (var candidate in tokens)
        {
            if (string.Equals(candidate, token, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    private static double Average(double sum, int count) => count == 0 ? 0 : sum / count;

    internal static FieldTokens Fields(CatalogItem item)
    {
        var target = Tokenizer.Tokenize(item.Target).ToArray();
        var name = Tokenizer.Tokenize(item.Resource?.Name ?? item.ResourceTemplate?.Name).ToArray();
        var description = Tokenizer.Tokenize(item.Description).ToArray();
        var parameters = ParameterTokens(item.Tool).ToArray();
        return new FieldTokens(target, name, description, parameters);
    }

    private static IEnumerable<string> ParameterTokens(Tool? tool)
    {
        if (tool is null)
            yield break;

        var schema = tool.InputSchema;
        if (schema.ValueKind != JsonValueKind.Object)
            yield break;
        if (!schema.TryGetProperty(McpSpecKeys.JsonSchema.Properties, out var properties)
            || properties.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var property in properties.EnumerateObject())
        foreach (var token in Tokenizer.Tokenize(property.Name))
            yield return token;
    }

    internal readonly record struct FieldTokens(string[] Target, string[] Name, string[] Description, string[] Parameters)
    {
        public IEnumerable<string> All
        {
            get
            {
                foreach (var token in Target) yield return token;
                foreach (var token in Name) yield return token;
                foreach (var token in Description) yield return token;
                foreach (var token in Parameters) yield return token;
            }
        }
    }
}

public sealed class ScoringCorpus
{
    public static ScoringCorpus Empty { get; } = new(0, new Dictionary<string, int>(), 0, 0, 0, 0);

    public ScoringCorpus(
        int documentCount,
        IReadOnlyDictionary<string, int> documentFrequency,
        double averageTargetLength,
        double averageNameLength,
        double averageDescriptionLength,
        double averageParameterLength)
    {
        DocumentCount = documentCount;
        DocumentFrequency = documentFrequency;
        AverageTargetLength = averageTargetLength;
        AverageNameLength = averageNameLength;
        AverageDescriptionLength = averageDescriptionLength;
        AverageParameterLength = averageParameterLength;
    }

    public int DocumentCount { get; }
    public IReadOnlyDictionary<string, int> DocumentFrequency { get; }
    public double AverageTargetLength { get; }
    public double AverageNameLength { get; }
    public double AverageDescriptionLength { get; }
    public double AverageParameterLength { get; }
}
