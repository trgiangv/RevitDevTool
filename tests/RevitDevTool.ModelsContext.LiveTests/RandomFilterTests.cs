using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Scope;
using Nice3point.Revit.Extensions;
using System.Globalization;
using System.Text.Json;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed class RandomConfig
{
    public int MaxDepth { get; set; } = 5;

    public int MaxRules { get; set; } = 50;

    public List<int> Seeds { get; set; } = [];
}

[TestFixture]
public sealed class RandomFilterTests : RecordedTest
{
    private const string CountVariable = "MODELS_CONTEXT_RANDOM_TREES";
    private const int LargeEvery = 25;
    private const int MaxCategoryElements = 2500;

    private static readonly Lazy<RandomConfig> LazyConfig = new(() => CaseFiles.Read<RandomConfig>("random-filters.json"));
    private static readonly Dictionary<string, List<long>> Pools = new(StringComparer.Ordinal);
    private static Adapter _adapter = null!;

    public static IEnumerable<TestCaseData> Trees => Enumerable.Range(1, TreeCount()).Select(number => new TestCaseData(Id(number)).SetArgDisplayNames(Id(number)));

    [OneTimeSetUp]
    public void Open()
    {
        _adapter = new Adapter();
        Pools.Clear();
    }

    [TestCaseSource(nameof(Trees))]
    public void TreeMatchesOracle(string id)
    {
        var number = int.Parse(id.Substring(3), CultureInfo.InvariantCulture);
        var config = LazyConfig.Value;
        var seed = config.Seeds[number - 1];
        var item = new CaseItem
        {
            Id = id,
            Description = $"Random filter tree, seed {seed}",
            Expected = JsonSerializer.SerializeToElement("panel = oracle"),
            ManualSteps =
            [
                "Run this tree again: the same seed gives the same tree on the same sample.",
                "On a mismatch the run folder holds random-failure-<id>.json: paste its case into filters.json and its parameters into sample-facts.json, or paste its stored filter into the custom filter file, and compare the panel with the expected oracle count."
            ]
        };
        Record(item, () =>
        {
            var random = new Random(seed);
            var docs = new[] { "Host", "Arch", "Both" }[random.Next(3)];
            var documents = SampleModel.Of(docs);
            var pool = Pool(docs, documents);
            if (pool.Count == 0)
            {
                Assert.Inconclusive($"No category of {docs} has between 1 and {MaxCategoryElements} elements.");
            }

            var groups = Groups(random, pool);
            var parameters = ParameterCatalog.Merge([.. documents.Select(document => Picks.Catalog(_adapter, document))])
                .Where(parameter => parameter.Categories.Overlaps(groups)).ToList();
            var budget = number % LargeEvery == 0
                ? config.MaxRules
                : 1 + (int)((config.MaxRules - 1) * Math.Pow(random.NextDouble(), 3));
            var builder = new TreeBuilder(random, _adapter, documents, parameters, groups, config.MaxDepth, SampleModel.KnownSources);
            var definition = Evidence.Time("build", () => builder.Build(budget));
            var (rules, depth) = Measure(definition.Root, 1);
            if (Evidence.Current is { } current)
            {
                current.Description = $"Random filter tree, seed {seed}: {docs}, {groups.Count} categories, {rules} rules, depth {depth}";
            }

            Evidence.Step($"Built from the real catalog and value index of {docs}: {groups.Count} categories, {parameters.Count} candidate parameters, {rules} rules, depth {depth}.");
            var oracle = Evidence.Time("oracle", () => Oracle.Matches(definition, documents, ScopeKind.WholeModel).ToHashSet());
            var panel = Evidence.Time("panel", () => _adapter.Matches(definition, documents, ScopeKind.WholeModel).ToHashSet());
            Evidence.Values(oracle.Count, panel.Count);
            Evidence.Step($"Oracle {oracle.Count:N0}, panel {panel.Count:N0}.");

            if (!panel.SetEquals(oracle))
            {
                var difference = $"oracle {oracle.Count}, panel {panel.Count}, {oracle.Except(panel).Count()} only in the oracle, {panel.Except(oracle).Count()} only in the panel";
                var path = FailureCase.Write(id, seed, docs, definition, oracle.Count, difference);
                Assert.Fail($"{id} (seed {seed}): {difference}. The tree is in {path}.");
            }
        });
    }

    private static int TreeCount()
    {
        var seeds = LazyConfig.Value.Seeds.Count;
        return int.TryParse(Environment.GetEnvironmentVariable(CountVariable), NumberStyles.Integer, CultureInfo.InvariantCulture, out var wanted) && wanted > 0
            ? Math.Min(wanted, seeds)
            : seeds;
    }

    private static string Id(int number)
    {
        return $"RT-{number:000}";
    }

    private static List<long> Pool(string docs, IReadOnlyList<SampleDocument> documents)
    {
        if (!Pools.TryGetValue(docs, out var pool))
        {
            var counts = new Dictionary<long, int>();
            foreach (var document in documents)
            {
                foreach (var group in Adapter.Run(_adapter.Source.CountGroupsAsync(document.Key, ScopeKind.WholeModel, CancellationToken.None)).Where(group => group.Count > 0))
                {
                    counts[group.GroupId] = counts.TryGetValue(group.GroupId, out var known) ? known + group.Count : group.Count;
                }
            }

            pool = [.. counts.Where(pair => pair.Value <= MaxCategoryElements).Select(pair => pair.Key).OrderBy(category => category)];
            Pools.Add(docs, pool);
        }

        return pool;
    }

    private static List<long> Groups(Random random, List<long> pool)
    {
        var groups = new List<long>();
        var wanted = Math.Min(pool.Count, 1 + random.Next(3));
        while (groups.Count < wanted)
        {
            var category = pool[random.Next(pool.Count)];
            if (!groups.Contains(category))
            {
                groups.Add(category);
            }
        }

        return groups;
    }

    private static (int Rules, int Depth) Measure(FilterSet set, int depth)
    {
        var rules = 0;
        var deepest = depth;
        foreach (var child in set.Children)
        {
            if (child is FilterSet nested)
            {
                var (nestedRules, nestedDepth) = Measure(nested, depth + 1);
                rules += nestedRules;
                deepest = Math.Max(deepest, nestedDepth);
            }
            else if (child is RuleNode)
            {
                rules++;
            }
        }

        return (rules, deepest);
    }
}
