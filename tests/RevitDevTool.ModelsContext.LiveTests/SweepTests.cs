using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Scope;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Pane;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class SweepTests : RecordedTest
{
    private const string LimitVariable = "MODELS_CONTEXT_SWEEP_LIMIT";
    private const string ScheduleVariable = "MODELS_CONTEXT_SWEEP_SCHEDULES";
    private const string TreesVariable = "MODELS_CONTEXT_SWEEP_TREES";
    private const string Explicit = "Sweeps the active model and every loaded link; run it alone by its method name.";
    private const int Shown = 20;
    private const int LargeEvery = 25;
    private const double MaxCallMs = 1000;

    private static readonly CancellationToken None = CancellationToken.None;

    private static readonly JsonSerializerOptions Output = new(CaseFiles.Options)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private static readonly Lazy<CaseFile<SweepSetup, CaseItem>> LazyData =
        new(() => CaseFiles.Read<CaseFile<SweepSetup, CaseItem>>("sweep.json"));

    private static readonly Dictionary<string, List<long>> Pools = new(StringComparer.Ordinal);

    private static SweepSetup Setup => LazyData.Value.Setup;

    public static IEnumerable<TestCaseData> Trees => Enumerable.Range(1, TreeCount()).Select(number => new TestCaseData(TreeId(number)).SetArgDisplayNames(TreeId(number)));

    [Test]
    [Explicit(Explicit)]
    public void SweepCountsMatchOracle()
    {
        Record(Areas.Find(LazyData.Value.Cases, "SW-01"), () =>
        {
            var adapter = new Adapter();
            var host = ActiveModel.Host;
            var problems = new List<string>();
            var compared = 0;
            Dictionary<long, int>? hostOracle = null;
            Dictionary<long, int>? hostPanel = null;
            foreach (var document in ActiveModel.Sources)
            {
                var oracle = Evidence.Time("oracle", () => Group(Oracle.Candidates(document.Document, [])));
                var panel = Evidence.Time("panel", () => Quick(adapter, document, ScopeKind.WholeModel));
                compared += Compare(problems, document, "Whole model", oracle, panel);
                if (document.Key.IsCurrentModel)
                {
                    hostOracle = oracle;
                    hostPanel = panel;
                }
            }

            var view = SampleModel.Application.ActiveUIDocument.ActiveView;
            if (DrawsModel(view))
            {
                foreach (var document in ActiveModel.Documents)
                {
                    var ids = Evidence.Time("oracle", () => Oracle.ViewIds(document, host));
                    var oracle = Evidence.Time("oracle", () => Group(Oracle.Candidates(document.Document, [], true).Where(element => ids.Contains(element.Id.ToLong()))));
                    var panel = Evidence.Time("panel", () => Quick(adapter, document, ScopeKind.ActiveView));
                    compared += Compare(problems, document, $"Active view '{view.Name}'", oracle, panel);
                }
            }
            else
            {
                Evidence.Step($"The active view '{view.Name}' ({view.ViewType}) is not a plan or 3D view: Active view counts skipped; the run never switches views.");
            }

            var schedules = new List<ScheduleRow>();
            if (hostOracle is not null && hostPanel is not null)
            {
                var skipped = 0;
                schedules = Evidence.Time("schedules", () => SweepSchedules.Count(host, hostOracle, hostPanel, SweepLimits.Read(ScheduleVariable, Setup.ScheduleLimit), out skipped));
                Evidence.Step($"Schedule reference: {skipped} schedulable categories of {host.Title} skipped by the cap ({ScheduleVariable}).");
            }

            Evidence.Step("Links are not scheduled: a link document cannot be written, and a schedule needs a transaction in its document.");
            var scheduleDiffers = schedules.Where(row => row.Reason != SweepNative.Same).ToList();
            Write("sweep-counts.json", new
            {
                run = Path.GetFileName(Evidence.Folder),
                model = host.Title,
                documents = ActiveModel.Documents.Select(document => document.Document.Title).ToList(),
                compared,
                mismatches = problems,
                schedules = new
                {
                    checkedCategories = schedules.Count,
                    equalToOracle = schedules.Count(row => row.Reason == SweepNative.Same),
                    differs = scheduleDiffers
                }
            });
            Evidence.Step($"Schedules: {schedules.Count} categories, {scheduleDiffers.Count} differ from the oracle (reasons in sweep-counts.json).");
            Evidence.Values($"{compared} category counts", problems.Count == 0 ? "all equal" : $"{problems.Count} differ");
            Assert.That(problems, Is.Empty, $"{problems.Count} category counts differ:\n" + string.Join("\n", problems.Take(Shown)));
        });
    }

    [Test]
    [Explicit(Explicit)]
    public void SweepParametersMatchOracle()
    {
        Record(Areas.Find(LazyData.Value.Cases, "SW-02"), () =>
        {
            var limit = SweepLimits.Read(LimitVariable, Setup.EvaluationLimit);
            var adapter = new Adapter();
            var host = ActiveModel.Host;
            var known = ActiveModel.Known;
            var units = host.GetUnits();
            var plan = new List<(SampleDocument Document, long Category, ParameterInfo Parameter)>();
            foreach (var document in ActiveModel.Sources)
            {
                var categories = Quick(adapter, document, ScopeKind.WholeModel).Where(pair => pair.Value > 0).Select(pair => pair.Key).OrderBy(id => id).ToList();
                var catalog = Picks.Catalog(adapter, document);
                foreach (var category in categories)
                {
                    plan.AddRange(catalog.Where(parameter => parameter.Categories.Contains(category)).Select(parameter => (document, category, parameter)));
                }
            }

            var stride = Math.Max(1, (int)Math.Ceiling(plan.Count * (double)SweepRuleSet.PerParameter / limit));
            var chosen = plan.Where((_, index) => index % stride == 0).ToList();
            Evidence.Step($"{plan.Count:N0} category parameters in {ActiveModel.Sources.Count} documents; limit {limit:N0} evaluations ({LimitVariable}), so every {stride}. one is checked: {chosen.Count:N0}, {plan.Count - chosen.Count:N0} skipped by the cap.");

            var rows = new List<SweepRow>();
            var evaluations = 0;
            var skippedByLimit = 0;
            var linkMedians = 0;
            foreach (var group in chosen.GroupBy(item => item.Document.Source))
            {
                var document = group.First().Document;
                var elements = new Dictionary<long, List<Element>>();
                var checks = new List<NativeCheck>();
                foreach (var (_, category, parameter) in group)
                {
                    if (evaluations >= limit)
                    {
                        skippedByLimit++;
                        continue;
                    }

                    if (!elements.TryGetValue(category, out var candidates))
                    {
                        candidates = [.. Oracle.Candidates(document.Document, [category])];
                        elements.Add(category, candidates);
                    }

                    var set = SweepRuleSet.For(adapter, document, category, parameter);
                    linkMedians += set.SkippedLinkMedian ? 1 : 0;
                    foreach (var rule in set.Rules)
                    {
                        evaluations++;
                        var (row, filter) = Evaluate(adapter, document, category, parameter, rule, candidates, known, units);
                        row.Note = set.Note ?? row.Note;
                        rows.Add(row);
                        checks.Add(new NativeCheck(row, filter, category));
                    }
                }

                if (document.Key.IsCurrentModel)
                {
                    Evidence.Time("view filters", () => SweepNative.ViewFilters(document.Document, checks));
                }
            }

            Evidence.Step($"{evaluations:N0} rule evaluations; {skippedByLimit:N0} more category parameters skipped by the cap.");
            if (linkMedians > 0)
            {
                Evidence.Step($"'At least the median' skipped for {linkMedians} number parameters in links: a typed number parses in the active model's units and a link's display text can read differently (PS-02), the rule RandomFilterTests follows.");
            }

            var mismatches = rows.Where(row => !row.PanelMatchesOracle).ToList();
            WriteMismatches(rows, mismatches, evaluations, limit, plan.Count - chosen.Count + skippedByLimit, linkMedians);
            Evidence.Values($"{evaluations:N0} rules, panel = oracle", mismatches.Count == 0 ? "all equal" : $"{mismatches.Count} differ");
            Assert.That(mismatches, Is.Empty, $"{mismatches.Count} rules differ (all in sweep-mismatches.json):\n"
                                              + string.Join("\n", mismatches.Take(Shown).Select(row => row.Line())));
        });
    }

    [TestCaseSource(nameof(Trees))]
    [Explicit(Explicit)]
    public void SweepTreesMatchOracle(string id)
    {
        var number = int.Parse(id.Substring(id.LastIndexOf('-') + 1), CultureInfo.InvariantCulture);
        var seed = Setup.Seeds[number - 1];
        var item = new CaseItem
        {
            Id = id,
            Description = $"Random filter tree over the loaded documents, seed {seed}",
            Expected = JsonSerializer.SerializeToElement("panel = oracle"),
            ManualSteps = [.. Areas.Find(LazyData.Value.Cases, "SW-03").ManualSteps]
        };
        Record(item, () =>
        {
            var adapter = new Adapter();
            var host = ActiveModel.Host;
            var known = ActiveModel.Known;
            var all = ActiveModel.Sources;
            var random = new Random(seed);
            var documents = all.OrderBy(_ => random.Next()).Take(1 + random.Next(Math.Min(3, all.Count))).ToList();
            var pool = Pool(adapter, documents);
            if (pool.Count == 0)
            {
                Assert.Inconclusive("The chosen documents have no category with elements.");
            }

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

            var parameters = ParameterCatalog.Merge([.. documents.Select(document => Picks.Catalog(adapter, document))])
                .Where(parameter => parameter.Categories.Overlaps(groups)).ToList();
            var budget = number % LargeEvery == 0
                ? Setup.MaxRules
                : 1 + (int)((Setup.MaxRules - 1) * Math.Pow(random.NextDouble(), 3));
            var definition = Evidence.Time("build", () => new TreeBuilder(random, adapter, documents, parameters, groups, Setup.MaxDepth, known).Build(budget));
            var titles = string.Join(", ", documents.Select(document => document.Document.Title));
            Evidence.Step($"Built from the catalogs and value lists of {titles}: {groups.Count} categories, {parameters.Count} candidate parameters, budget {budget} rules.");
            var oracle = Evidence.Time("oracle", () => Oracle.Matches(definition, documents, ScopeKind.WholeModel, host, known).ToHashSet());
            var panel = Evidence.Time("panel", () => adapter.Matches(definition, documents, ScopeKind.WholeModel, known).ToHashSet());
            Evidence.Values(oracle.Count, panel.Count);
            if (!panel.SetEquals(oracle))
            {
                var difference = $"oracle {oracle.Count}, panel {panel.Count}, {oracle.Except(panel).Count()} only in the oracle, {panel.Except(oracle).Count()} only in the panel";
                var path = Path.Combine(Evidence.Folder, $"sweep-tree-{id}.json");
                File.WriteAllText(path, JsonSerializer.Serialize(new
                {
                    id,
                    seed,
                    documents = documents.Select(document => document.Document.Title).ToList(),
                    expected = oracle.Count,
                    difference,
                    stored = StoredFilters.Write([new CustomFilter(id, definition)])
                }, Output));
                Assert.Fail($"{id} (seed {seed}): {difference}. The tree is in {path}.");
            }
        });
    }

    [Test]
    [Explicit(Explicit)]
    public void SweepTimingsPerDocument()
    {
        Record(Areas.Find(LazyData.Value.Cases, "SW-04"), () =>
        {
            var host = ActiveModel.Host;
            var sources = ActiveModel.Sources;
            var editable = new FilteredElementCollector(host).WhereElementIsNotElementType()
                .FirstOrDefault(element => element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS) is { IsReadOnly: false });
            Evidence.Step(editable is null
                ? "No element of the active model takes a comment: invalidated means the caches dropped, without an edit."
                : "Invalidated: a comment set on one element of the active model in a transaction that is rolled back, then the caches dropped.");
            var lines = new TraceLines();
            var previous = RevitProfiler.Logger;
            RevitProfiler.Use(lines);
            var perf = new SweepPerf(lines);
            try
            {
                for (var index = 0; index < sources.Count; index++)
                {
                    perf.Operations(sources[index], $"D{index + 1}", editable);
                }
            }
            finally
            {
                RevitProfiler.Use(previous);
            }

            var samples = perf.Samples;
            var longest = samples.OrderByDescending(sample => sample.LongestCallMs).FirstOrDefault();
            Write("sweep-perf.json", new
            {
                run = Path.GetFileName(Evidence.Folder),
                revit = SampleModel.Application.Application.VersionNumber,
                written = DateTime.Now,
                thresholds = new { instantMs = 100, noticeableMs = 300, slowMs = 1000, hitchMs = SweepPerf.HitchMs, maxCallMs = MaxCallMs },
                documents = sources.Select((document, index) => new { label = $"D{index + 1}", title = document.Document.Title, link = !document.Key.IsCurrentModel }).ToList(),
                longestCall = longest is null ? null : new { name = longest.Key, ms = longest.LongestCallMs },
                hitches = samples.Where(sample => sample.Hitch).Select(sample => $"{sample.Key} {sample.LongestCallMs} ms").ToList(),
                ratings = samples.GroupBy(sample => sample.Rating).ToDictionary(group => group.Key, group => group.Count()),
                samples
            });
            var slow = samples.Where(sample => sample.LongestCallMs > MaxCallMs).Select(sample => $"{sample.Key}: one Revit call took {sample.LongestCallMs} ms").ToList();
            Evidence.Values($"no Revit call over {MaxCallMs} ms", longest is null ? "nothing measured" : $"longest {longest.Key} {longest.LongestCallMs} ms");
            Assert.That(slow, Is.Empty, string.Join("\n", slow));
        });
    }

    private static (SweepRow Row, ElementFilter? Filter) Evaluate(Adapter adapter, SampleDocument document, long category, ParameterInfo parameter,
        RuleNode rule, List<Element> candidates, ICollection<SourceId> known, Units units)
    {
        var row = new SweepRow
        {
            Document = document.Document.Title,
            Category = ActiveModel.CategoryName(document.Document, category),
            Parameter = parameter.DisplayName,
            BuiltIn = parameter.Key.BuiltInName,
            Kind = parameter.Kind.ToString(),
            Operator = rule.Operator.ToString(),
            Value = rule.Value
        };
        var definition = new FilterDefinition([category], FilterSet.All(rule));
        HashSet<long> oracle;
        HashSet<long> panel;
        try
        {
            oracle = [.. candidates.Where(element => Oracle.Matches(definition.Root, element, document.Source, known, units)).Select(element => element.Id.ToLong())];
        }
        catch (Exception exception) when (exception is not AssertionException)
        {
            row.Oracle = -1;
            row.Note = "oracle failed: " + exception.Message;
            return (row, null);
        }

        try
        {
            panel = [.. Adapter.Run(adapter.Evaluator.EvaluateAsync(document.Key, definition, null, None))];
        }
        catch (Exception exception) when (exception is not AssertionException)
        {
            row.Oracle = oracle.Count;
            row.Panel = -1;
            row.Note = "panel failed: " + exception.Message;
            return (row, null);
        }

        row.Oracle = oracle.Count;
        row.Panel = panel.Count;
        row.OnlyOracle = [.. oracle.Except(panel).OrderBy(id => id).Take(5)];
        row.OnlyPanel = [.. panel.Except(oracle).OrderBy(id => id).Take(5)];
        row.Facts = [.. row.OnlyOracle.Take(1).Concat(row.OnlyPanel.Take(1)).Select(id => SweepNative.Facts(document.Document, rule.Parameter, id))];
        var filter = SweepNative.Run(document, category, rule, parameter, panel, row);
        return (row, filter);
    }

    private static void WriteMismatches(List<SweepRow> rows, List<SweepRow> mismatches, int evaluations, int limit, int skipped, int linkMedians)
    {
        var agreeing = rows.Where(row => row.PanelMatchesOracle).ToList();
        var nativeDiffers = agreeing.Where(row => row.Native is not null && row.Reason != SweepNative.Same).ToList();
        Write("sweep-mismatches.json", new
        {
            run = Path.GetFileName(Evidence.Folder),
            evaluations,
            limit,
            skippedByLimit = skipped,
            skippedLinkMedians = linkMedians,
            panelOracleMismatches = mismatches.Count,
            mismatches,
            native = new
            {
                panelOracleNativeEqual = agreeing.Count(row => row.Reason == SweepNative.Same),
                panelOracleDifferFromNative = nativeDiffers.Count,
                byReason = nativeDiffers.GroupBy(row => row.Reason).OrderBy(group => group.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count()),
                notExpressible = agreeing.Where(row => row.Native is null).GroupBy(row => row.Reason).OrderBy(group => group.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count()),
                unexplained = nativeDiffers.Where(row => row.Reason == SweepNative.Unexplained).ToList(),
                viewFilters = new
                {
                    created = rows.Count(row => row.ViewFilter is not null),
                    equalToNative = rows.Count(row => row.ViewFilter is not null && row.ViewFilter == row.Native),
                    differFromNative = rows.Where(row => row.ViewFilter is not null && row.ViewFilter != row.Native).ToList(),
                    notCreated = rows.Where(row => row.ViewFilterNote is not null).GroupBy(row => row.ViewFilterNote!).OrderBy(group => group.Key, StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => group.Count())
                }
            }
        });
        Evidence.Step($"Native: {agreeing.Count(row => row.Reason == SweepNative.Same):N0} rules panel = oracle = native, {nativeDiffers.Count:N0} differ from native ({nativeDiffers.Count(row => row.Reason == SweepNative.Unexplained):N0} unexplained), {agreeing.Count(row => row.Native is null):N0} not expressible.");
    }

    private static int Compare(List<string> problems, SampleDocument document, string scope, Dictionary<long, int> oracle, Dictionary<long, int> panel)
    {
        var keys = oracle.Keys.Union(panel.Keys).OrderBy(key => key).ToList();
        foreach (var key in keys)
        {
            var left = oracle.TryGetValue(key, out var found) ? found : 0;
            var right = panel.TryGetValue(key, out var counted) ? counted : 0;
            if (left != right)
            {
                problems.Add($"{document.Document.Title} / {scope} / {ActiveModel.CategoryName(document.Document, key)}: oracle {left}, panel {right}");
            }
        }

        Evidence.Step($"{document.Document.Title}, {scope}: {keys.Count} categories, oracle {oracle.Values.Sum():N0}, panel {panel.Values.Sum():N0} elements.");
        return keys.Count;
    }

    private static Dictionary<long, int> Group(IEnumerable<Element> elements)
    {
        return elements.GroupBy(element => element.Category.Id.ToLong()).ToDictionary(group => group.Key, group => group.Count());
    }

    private static Dictionary<long, int> Quick(Adapter adapter, SampleDocument document, ScopeKind scope)
    {
        return Adapter.Run(adapter.Source.CountGroupsAsync(document.Key, scope, None))
            .Where(group => group.Count > 0)
            .ToDictionary(group => group.GroupId, group => group.Count);
    }

    private static bool DrawsModel(View view)
    {
        return !view.IsTemplate && view.ViewType is ViewType.FloorPlan or ViewType.CeilingPlan or ViewType.EngineeringPlan
            or ViewType.AreaPlan or ViewType.ThreeD;
    }

    private static List<long> Pool(Adapter adapter, IReadOnlyList<SampleDocument> documents)
    {
        var key = string.Join("|", documents.Select(document => document.Source.Value));
        if (!Pools.TryGetValue(key, out var pool))
        {
            pool = [.. documents.SelectMany(document => Quick(adapter, document, ScopeKind.WholeModel).Keys).Distinct().OrderBy(category => category)];
            Pools.Add(key, pool);
        }

        return pool;
    }

    private static int TreeCount()
    {
        var seeds = LazyData.Value.Setup.Seeds.Count;
        return Math.Min(SweepLimits.Read(TreesVariable, seeds), seeds);
    }

    private static string TreeId(int number)
    {
        return $"SW-03-{number:000}";
    }

    private static void Write(string name, object content)
    {
        File.WriteAllText(Path.Combine(Evidence.Folder, name), JsonSerializer.Serialize(content, Output));
    }
}
