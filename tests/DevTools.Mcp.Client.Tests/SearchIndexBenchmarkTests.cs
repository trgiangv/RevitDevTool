using Bogus;
using ModelContextProtocol.Protocol;
using Moq;
using System.Diagnostics;
using System.Text.Json;
using SearchTokenizer = DevTools.Daemon.Mcp.Search.Tokenizer;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class SearchIndexBenchmarkTests
{
    private const int ItemCountPerKind = 1_000;
    private const int QueryCountPerSample = 20;
    private const int SampleCount = 5;
    private const int ResultLimit = 8;
    private static readonly string[] Queries =
    [
        "fire rating doors",
        "export schedule excel",
        "warning severity description",
        "family type parameters",
        "place view on sheet",
        "element identifier",
        "read room area",
        "find linked model",
        "update level elevation",
        "unknown vocabulary",
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ProcessCatalogSearch_MatchesFullSort_AndReportsRealisticWorkloadTiming()
    {
        var (catalog, items) = CreateCatalog();
        var processCatalogs = new ProcessCatalogs();
        processCatalogs.Replace(catalog);

        var fields = items.Select(Scoring.Fields).ToArray();
        var corpus = Scoring.Build(fields);
        var candidatesByToken = BuildCandidatesByToken(fields);

        var rebuildSamples = MeasureRebuilds(processCatalogs, catalog);
        AssertSearchMatchesFullSort(processCatalogs, items, fields, corpus, candidatesByToken);
        var searchSamples = MeasureSearches(processCatalogs, items, fields, corpus, candidatesByToken);
        WriteBenchmarkResults(TestContext, catalog, items, candidatesByToken, rebuildSamples, searchSamples);
    }

    private static List<TimeSpan> MeasureRebuilds(ProcessCatalogs catalogs, ProcessCatalog catalog)
    {
        for (var warmup = 0; warmup < 2; warmup++)
            catalogs.Replace(catalog);

        return Enumerable.Range(0, SampleCount).Select(_ => Measure(() => catalogs.Replace(catalog))).ToList();
    }

    private static void AssertSearchMatchesFullSort(
        ProcessCatalogs catalogs,
        CatalogItem[] items,
        Scoring.FieldTokens[] fields,
        ScoringCorpus corpus,
        Dictionary<string, List<int>> candidatesByToken)
    {
        foreach (var query in Queries)
        {
            var expected = SearchByFullSort(items, fields, corpus, candidatesByToken, query);
            var actual = catalogs.Search(query, [CatalogType.Tool], limit: ResultLimit);
            Assert.AreSequenceEqual(
                expected.Select(item => item.Target).ToArray(),
                actual.Select(match => match.Item.Target).ToArray(),
                query);
        }
    }

    private static SearchSamples MeasureSearches(
        ProcessCatalogs catalogs,
        CatalogItem[] items,
        Scoring.FieldTokens[] fields,
        ScoringCorpus corpus,
        Dictionary<string, List<int>> candidatesByToken)
    {
        for (var warmup = 0; warmup < 10; warmup++)
        {
            RunIndexedBatch(catalogs);
            RunFullSortBatch(items, fields, corpus, candidatesByToken);
        }

        var indexed = new List<TimeSpan>(SampleCount);
        var fullSort = new List<TimeSpan>(SampleCount);
        for (var sample = 0; sample < SampleCount; sample++)
            MeasureSearchPair(sample, catalogs, items, fields, corpus, candidatesByToken, indexed, fullSort);

        return new SearchSamples(Median(indexed), Median(fullSort));
    }

    private static void MeasureSearchPair(
        int sample,
        ProcessCatalogs catalogs,
        CatalogItem[] items,
        Scoring.FieldTokens[] fields,
        ScoringCorpus corpus,
        Dictionary<string, List<int>> candidatesByToken,
        List<TimeSpan> indexedSamples,
        List<TimeSpan> fullSortSamples)
    {
        var indexedSearch = (Action)(() => RunIndexedBatch(catalogs));
        var fullSortSearch = (Action)(() => RunFullSortBatch(items, fields, corpus, candidatesByToken));
        var indexedFirst = sample % 2 == 0;
        var first = Measure(indexedFirst ? indexedSearch : fullSortSearch);
        var second = Measure(indexedFirst ? fullSortSearch : indexedSearch);
        indexedSamples.Add(indexedFirst ? first : second);
        fullSortSamples.Add(indexedFirst ? second : first);
    }

    private static void WriteBenchmarkResults(
        TestContext testContext,
        ProcessCatalog catalog,
        CatalogItem[] items,
        Dictionary<string, List<int>> candidatesByToken,
        IReadOnlyCollection<TimeSpan> rebuildSamples,
        SearchSamples searchSamples)
    {
        var averageDescriptionLength = catalog.Tools.Average(tool => tool.Description?.Length ?? 0);
        var totalCandidates = Queries.Sum(query => CountCandidates(items, candidatesByToken, query, CatalogType.Tool));
        testContext.WriteLine(
            $"ProcessCatalogs.Search workload: {ItemCountPerKind:N0} MCP tools + {ItemCountPerKind:N0} MCP resources; "
            + $"tool description avg: {averageDescriptionLength:N0} chars; "
            + $"queries: {Queries.Length} ({totalCandidates:N0} candidate set members across the query set); "
            + $"default result limit: {ResultLimit}; warm rebuild median ({SampleCount} samples): "
            + $"{Median(rebuildSamples).TotalMilliseconds:N1} ms; search median ({SampleCount} × {QueryCountPerSample} queries): "
            + $"indexed top-K: {searchSamples.Indexed.TotalMilliseconds:N1} ms, "
            + $"full-sort reference: {searchSamples.FullSort.TotalMilliseconds:N1} ms, "
            + $"speedup: {searchSamples.FullSort.TotalMilliseconds / searchSamples.Indexed.TotalMilliseconds:N2}x.");
    }

    [TestMethod]
    public void SearchIndexRebuild_EnumeratesCatalogSourceOnce()
    {
        var source = new Mock<IEnumerable<CatalogItem>>();
        using var enumerator = Enumerable.Empty<CatalogItem>().GetEnumerator();
        source.Setup(items => items.GetEnumerator()).Returns(enumerator);

        new SearchIndex().Rebuild(source.Object);

        source.Verify(items => items.GetEnumerator(), Times.Once);
    }

    private static (ProcessCatalog Catalog, CatalogItem[] Items) CreateCatalog()
    {
        Randomizer.Seed = new Random(4_020_261);
        var faker = new Faker();
        var tools = new List<Tool>(ItemCountPerKind);
        var resources = new List<Resource>(ItemCountPerKind);
        var items = new List<CatalogItem>(ItemCountPerKind * 2);
        for (var index = 0; index < ItemCountPerKind; index++)
        {
            var profile = ToolProfiles[index % ToolProfiles.Length];
            var name = $"{profile.Name}_{index:D4}";
            var description = CreateToolDescription(faker, profile, index);
            var inputSchema = CreateInputSchema(profile);
            var outputSchema = CreateOutputSchema(profile);
            var tool = new Tool
            {
                Name = name,
                Description = description,
                InputSchema = inputSchema,
                OutputSchema = outputSchema,
            };
            tools.Add(tool);
            items.Add(new CatalogItem(CatalogType.Tool, name, description, 10_001, null!, Tool: tool));

            var resourceName = $"{profile.ResourceName}_{index:D4}";
            var uri = $"revit://model/{index:D4}/{profile.ResourceName}";
            var resourceDescription = $"{profile.ResourcePurpose} for model element {index:D4}; includes identifiers, "
                + $"category, type, document context, and read-only inspection details. {faker.Commerce.ProductAdjective()} model snapshot.";
            var resource = new Resource { Name = resourceName, Uri = uri, Description = resourceDescription };
            resources.Add(resource);
            items.Add(new CatalogItem(CatalogType.Resource, uri, resourceDescription, 10_001, null!, Resource: resource));
        }

        // ProcessCatalogs indexes all standard MCP item kinds in enum order: tools, resources, templates.
        items = items.OrderBy(item => item.Kind).ToList();
        var catalog = new ProcessCatalog
        {
            ProcessId = 10_001,
            Instance = new InstanceInfo { HostApp = "Revit", ProcessId = 10_001, VersionNumber = "2025" },
            PipeName = "benchmark",
            Tools = tools,
            Resources = resources,
            ResourceTemplates = [],
        };
        return (catalog, items.ToArray());
    }

    private static string CreateToolDescription(Faker faker, ToolProfile profile, int index)
    {
        var action = faker.PickRandom(profile.UsageGuidance);
        var filter = faker.PickRandom(
            "Filter by category, family, type, level, phase, and workset when those constraints matter.",
            "Pass stable element identifiers when the request refers to specific model objects.",
            "Use a narrow category or view scope first, then inspect individual results as needed.");
        var result = faker.PickRandom(profile.ResultGuidance);
        var cautions = faker.PickRandom(
            "This operation reads the active document and does not modify model state.",
            "Large result sets should be narrowed by category or identifier before returning details.",
            "Returned values follow the host document units and preserve the element's model context.");
        var context = faker.Lorem.Sentence(8);
        return $"{profile.Purpose} {action} {filter} {result} {cautions} "
            + $"Tool instance {index:D4} supports {profile.Domain} inspection in a Revit document. "
            + $"Additional host note: {context}";
    }

    private static JsonElement CreateInputSchema(ToolProfile profile)
    {
        var properties = new Dictionary<string, object>();
        foreach (var parameter in profile.Parameters)
        {
            properties[parameter.Name] = new
            {
                type = parameter.Type,
                description = parameter.Description,
                @enum = parameter.EnumValues,
            };
        }

        return JsonSerializer.SerializeToElement(new { type = "object", properties, required = profile.Required });
    }

    private static JsonElement CreateOutputSchema(ToolProfile profile)
    {
        var properties = new Dictionary<string, object>
        {
            [profile.OutputName] = new { type = "array", description = profile.OutputDescription },
            ["elementIds"] = new { type = "array", description = "Stable identifiers for matching Revit elements." },
        };
        return JsonSerializer.SerializeToElement(new { type = "object", properties });
    }

    private static Dictionary<string, List<int>> BuildCandidatesByToken(Scoring.FieldTokens[] fields)
    {
        var index = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var document = 0; document < fields.Length; document++)
            foreach (var token in fields[document].All)
            {
                if (!index.TryGetValue(token, out var documents))
                    index[token] = documents = [];
                documents.Add(document);
            }

        return index;
    }

    private static CatalogItem[] SearchByFullSort(
        CatalogItem[] items,
        Scoring.FieldTokens[] fields,
        ScoringCorpus corpus,
        Dictionary<string, List<int>> candidatesByToken,
        string query)
    {
        var tokens = SearchTokenizer.Distinct(SearchTokenizer.Tokenize(query));
        var candidates = new HashSet<int>();
        foreach (var token in tokens)
        {
            if (!candidatesByToken.TryGetValue(token, out var bucket))
                continue;
            foreach (var index in bucket)
                if (items[index].Kind == CatalogType.Tool)
                    candidates.Add(index);
        }

        return candidates
            .Select(index => (
                Index: index,
                Item: items[index],
                Score: Scoring.Score(tokens, fields[index], corpus),
                Coverage: SearchPolicy.QueryCoverage(tokens, fields[index], corpus)))
            .Where(candidate => candidate.Score > 0 && candidate.Coverage >= SearchPolicy.MinimumCoverage)
            .OrderByDescending(candidate => candidate.Coverage)
            .ThenByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Item.Kind)
            .ThenBy(candidate => candidate.Item.Target, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Item.ProcessId)
            .ThenBy(candidate => candidate.Index)
            .Take(ResultLimit)
            .Select(candidate => candidate.Item)
            .ToArray();
    }

    private static int CountCandidates(
        CatalogItem[] items,
        Dictionary<string, List<int>> candidatesByToken,
        string query,
        CatalogType kind)
    {
        var candidates = new HashSet<int>();
        foreach (var token in SearchTokenizer.Distinct(SearchTokenizer.Tokenize(query)))
            if (candidatesByToken.TryGetValue(token, out var bucket))
                foreach (var index in bucket)
                    if (items[index].Kind == kind)
                        candidates.Add(index);
        return candidates.Count;
    }

    private static void RunIndexedBatch(ProcessCatalogs catalogs)
    {
        for (var index = 0; index < QueryCountPerSample; index++)
            catalogs.Search(Queries[index % Queries.Length], [CatalogType.Tool], limit: ResultLimit);
    }

    private static void RunFullSortBatch(
        CatalogItem[] items,
        Scoring.FieldTokens[] fields,
        ScoringCorpus corpus,
        Dictionary<string, List<int>> candidatesByToken)
    {
        for (var index = 0; index < QueryCountPerSample; index++)
            SearchByFullSort(items, fields, corpus, candidatesByToken, Queries[index % Queries.Length]);
    }

    private static TimeSpan Measure(Action search)
    {
        var stopwatch = Stopwatch.StartNew();
        search();
        return stopwatch.Elapsed;
    }

    private static TimeSpan Median(IReadOnlyCollection<TimeSpan> samples) =>
        samples.OrderBy(sample => sample).ElementAt(samples.Count / 2);

    private sealed record SearchSamples(TimeSpan Indexed, TimeSpan FullSort);

    private sealed record ParameterProfile(string Name, string Type, string Description, string[] EnumValues);

    private sealed record ToolProfile(
        string Name,
        string Purpose,
        string Domain,
        string ResourceName,
        string ResourcePurpose,
        string[] UsageGuidance,
        string[] ResultGuidance,
        ParameterProfile[] Parameters,
        string[] Required,
        string OutputName,
        string OutputDescription);

    private static readonly ToolProfile[] ToolProfiles =
    [
        Profile(
            "find_doors_by_fire_rating", "Find doors and inspect fire rating parameters", "door fire rating",
            "doors", "Door and fire rating data", "fireRating", "Fire protection classifications"),
        Profile(
            "export_schedule_data", "Export schedule rows with fields and formatting", "schedule export",
            "schedules", "Schedule definitions and rows", "scheduleName", "Tabular schedule data"),
        Profile(
            "find_model_warnings", "Find warnings by severity and description", "warning severity",
            "warnings", "Model warning summaries", "severity", "Warning descriptions and affected elements"),
        Profile(
            "read_family_type_parameters", "Read family and type parameter values", "family type parameter",
            "families", "Family and type definitions", "parameterNames", "Family type parameter values"),
        Profile(
            "place_view_on_sheet", "Place a view into a sheet viewport", "view sheet placement",
            "sheets", "Sheet and viewport relationships", "sheetNumber", "View placement details"),
        Profile(
            "inspect_element_identifiers", "Inspect stable element identifiers and categories", "element identifier",
            "elements", "Element identity and category data", "elementIds", "Element identifiers and categories"),
        Profile(
            "read_room_area", "Read room boundaries, names, and area values", "room area",
            "rooms", "Room boundary and area data", "roomArea", "Room area and boundary values"),
        Profile(
            "find_linked_models", "Find linked models and their load status", "linked model",
            "links", "Linked document names and status", "linkStatus", "Linked model status"),
        Profile(
            "update_level_elevation", "Update level elevation and related constraints", "level elevation",
            "levels", "Level elevation and datum data", "elevation", "Level elevation values"),
        Profile(
            "read_view_performance", "Read view visibility and performance details", "view performance",
            "views", "View settings and visible categories", "viewDetail", "Visibility and view performance data"),
    ];

    private static ToolProfile Profile(
        string name,
        string purpose,
        string domain,
        string resourceName,
        string resourcePurpose,
        string specialParameter,
        string outputName) => new(
        name,
        purpose,
        domain,
        resourceName,
        resourcePurpose,
        [
            $"Use this capability when the requested result is about {domain}.",
            $"Apply the requested {domain} conditions before returning data.",
        ],
        [
            $"Return matching {resourceName} with concise identifiers and values.",
            $"Include the requested {domain} evidence in each result.",
        ],
        [
            new ParameterProfile(
                specialParameter,
                "string",
                $"Select records using {domain} terms and model context.",
                ["Standard", "High", "Unknown"]),
            new ParameterProfile(
                "category",
                "string",
                "Limit the search to a Revit category such as doors, walls, or rooms.",
                ["Doors", "Walls", "Rooms"]),
            new ParameterProfile(
                "elementIds",
                "array",
                "Optional stable identifiers for specific model elements.",
                []),
        ],
        [specialParameter],
        outputName,
        $"Matching {domain} values and related Revit element references.");
}
