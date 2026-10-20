using DevTools.Daemon.Mcp.Processes;
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable UseCollectionExpression

namespace DevTools.Daemon.Mcp.Search;

/// <summary>BM25F over catalog identity, descriptions, and standard MCP schemas.</summary>
public static class Scoring
{
    public const double K1 = 1.2;
    public const double B = 0.75;

    public static ScoringCorpus Build(IReadOnlyList<CatalogItem> items) => Build(items.Select(Fields).ToArray());

    internal static ScoringCorpus Build(IReadOnlyList<FieldTokens> documents)
    {
        var frequency = new Dictionary<string, int>(StringComparer.Ordinal);
        double target = 0;
        double name = 0;
        double description = 0;
        double parameters = 0;
        double schema = 0;

        foreach (var fields in documents)
        {
            target += fields.TargetLength;
            name += fields.NameLength;
            description += fields.DescriptionLength;
            parameters += fields.ParameterLength;
            schema += fields.SchemaLength;

            foreach (var token in fields.All)
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
            Average(parameters, n),
            Average(schema, n));
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

    private static double WeightedTermFrequency(string token, FieldTokens fields, ScoringCorpus corpus)
    {
        if (!fields.Frequencies.TryGetValue(token, out var counts))
            return 0;

        return Term(4, counts.Target, fields.TargetLength, corpus.AverageTargetLength)
               + Term(2, counts.Name, fields.NameLength, corpus.AverageNameLength)
               + Term(1, counts.Description, fields.DescriptionLength, corpus.AverageDescriptionLength)
               + Term(1, counts.Parameters, fields.ParameterLength, corpus.AverageParameterLength)
               + Term(0.5, counts.Schema, fields.SchemaLength, corpus.AverageSchemaLength);
    }

    private static double Term(double weight, int tf, int length, double averageLength)
    {
        if (tf == 0)
            return 0;

        var average = averageLength <= 0 ? 1 : averageLength;
        var norm = 1 - B + (B * length / average);
        return weight * tf / norm;
    }

    internal static double Idf(string token, ScoringCorpus corpus)
    {
        corpus.DocumentFrequency.TryGetValue(token, out var df);
        var n = corpus.DocumentCount;
        return Math.Log(1 + ((n - df + 0.5) / (df + 0.5)));
    }

    private static double Average(double sum, int count) => count == 0 ? 0 : sum / count;

    internal static FieldTokens Fields(CatalogItem item)
    {
        var target = Tokenizer.Tokenize(item.Target);
        var name = Tokenizer.Tokenize(item.Resource?.Name ?? item.ResourceTemplate?.Name);
        var description = Tokenizer.Tokenize(item.Description);
        var frequencies = new Dictionary<string, FieldFrequency>(StringComparer.Ordinal);

        AddTokens(frequencies, target, SearchField.Target);
        AddTokens(frequencies, name, SearchField.Name);
        AddTokens(frequencies, description, SearchField.Description);

        var (parameterLength, schemaLength, supportsArbitraryParameterFilter) = item.Tool is null
            ? default
            : SchemaIndexer.Index(item.Tool.InputSchema, item.Tool.OutputSchema, frequencies);

        return new FieldTokens(
            target.Count,
            name.Count,
            description.Count,
            parameterLength,
            schemaLength,
            frequencies,
            supportsArbitraryParameterFilter);
    }

    internal static int AddTokens(
        Dictionary<string, FieldFrequency> frequencies,
        IReadOnlyList<string> tokens,
        SearchField field)
    {
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            frequencies.TryGetValue(token, out var counts);
            frequencies[token] = counts.Increment(field);
        }

        return tokens.Count;
    }

    internal enum SearchField
    {
        Target,
        Name,
        Description,
        Parameters,
        Schema,
    }

    internal readonly record struct FieldFrequency(int Target, int Name, int Description, int Parameters, int Schema)
    {
        public FieldFrequency Increment(SearchField field) => field switch
        {
            SearchField.Target => this with { Target = Target + 1 },
            SearchField.Name => this with { Name = Name + 1 },
            SearchField.Description => this with { Description = Description + 1 },
            SearchField.Parameters => this with { Parameters = Parameters + 1 },
            SearchField.Schema => this with { Schema = Schema + 1 },
            _ => this,
        };
    }

    internal readonly record struct FieldTokens(
        int TargetLength,
        int NameLength,
        int DescriptionLength,
        int ParameterLength,
        int SchemaLength,
        IReadOnlyDictionary<string, FieldFrequency> Frequencies,
        bool SupportsArbitraryParameterFilter)
    {
        public IEnumerable<string> All => Frequencies.Keys;
    }
}

public sealed class ScoringCorpus(
    int documentCount,
    IReadOnlyDictionary<string, int> documentFrequency,
    double averageTargetLength,
    double averageNameLength,
    double averageDescriptionLength,
    double averageParameterLength,
    double averageSchemaLength)
{
    public static ScoringCorpus Empty { get; } = new(0, new Dictionary<string, int>(), 0, 0, 0, 0, 0);

    public int DocumentCount { get; } = documentCount;
    public IReadOnlyDictionary<string, int> DocumentFrequency { get; } = documentFrequency;
    public double AverageTargetLength { get; } = averageTargetLength;
    public double AverageNameLength { get; } = averageNameLength;
    public double AverageDescriptionLength { get; } = averageDescriptionLength;
    public double AverageParameterLength { get; } = averageParameterLength;
    public double AverageSchemaLength { get; } = averageSchemaLength;
}
