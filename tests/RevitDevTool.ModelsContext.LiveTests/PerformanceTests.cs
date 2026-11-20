using DevTools.ModelsContext.Model;
using Microsoft.Extensions.Logging;
using RevitDevTool.Tools.ModelsContext;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed class OperationBudget
{
    public double ColdMs { get; set; }

    public double WarmMs { get; set; }
}

internal sealed class ValueTarget
{
    public string Docs { get; set; } = string.Empty;

    public string Parameter { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;
}

internal sealed class RepeatSetup
{
    public string Filter { get; set; } = string.Empty;

    public int Runs { get; set; }
}

internal sealed class PerfBudgets
{
    public double HitchMs { get; set; }

    public double MaxCallMs { get; set; }

    public double SnapshotMbPerMillion { get; set; }

    public double RepeatGrowthMb { get; set; }

    public double RegressionPercent { get; set; }

    public double RegressionMs { get; set; }

    public Dictionary<string, OperationBudget> Operations { get; set; } = [];

    public List<string> Filters { get; set; } = [];

    public List<ValueTarget> Values { get; set; } = [];

    public RepeatSetup Repeat { get; set; } = new();
}

internal sealed class PerfSample
{
    public string Name { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public double Ms { get; set; }

    public double QueueMs { get; set; }

    public double RunMs { get; set; }

    public double LongestCallMs { get; set; }

    public int Calls { get; set; }

    public int Count { get; set; }

    public string Rating { get; set; } = string.Empty;

    public bool Hitch { get; set; }

    public double BudgetMs { get; set; }

    public double? BaselineMs { get; set; }

    public double? CompileMs { get; set; }

    public double? CandidatesMs { get; set; }

    public string? CandidateSource { get; set; }

    public double? MemoryBeforeMb { get; set; }

    public double? MemoryAfterMb { get; set; }

    public double? SnapshotMbPerMillion { get; set; }

    public double? MeasuredMbPerMillion { get; set; }

    public string Key => $"{Name}.{Phase}";
}

internal sealed record PerfResult(int Count, object? Keep = null);

internal sealed class TraceTotals
{
    private static readonly Regex Single = new(@"^Revit (?<op>.+?): queue (?<q>[\d.,]+) ms, run (?<r>[\d.,]+) ms$", RegexOptions.Compiled);

    private static readonly Regex Sliced = new(@"^Revit (?<head>.+?): (?<items>\d+) items in (?<slices>\d+) slices, queue (?<q>[\d.,]+) ms, run (?<r>[\d.,]+) ms \(max (?<m>[\d.,]+) ms\)", RegexOptions.Compiled);

    private static readonly Regex Compiled = new(@"^Filter compiled .+?: .*compile (?<c>[\d.,]+) ms, candidates (?<k>[\d.,]+) ms \((?<s>\w+)\)$", RegexOptions.Compiled);

    public double QueueMs { get; private set; }

    public double RunMs { get; private set; }

    public double LongestCallMs { get; private set; }

    public int Calls { get; private set; }

    public double? CompileMs { get; private set; }

    public double? CandidatesMs { get; private set; }

    public string? CandidateSource { get; private set; }

    public static TraceTotals Of(IEnumerable<string> lines)
    {
        var totals = new TraceTotals();
        foreach (var line in lines)
        {
            if (Sliced.Match(line) is { Success: true } sliced)
            {
                totals.QueueMs += Number(sliced, "q");
                totals.RunMs += Number(sliced, "r");
                totals.LongestCallMs = Math.Max(totals.LongestCallMs, Number(sliced, "m"));
                totals.Calls += int.Parse(sliced.Groups["slices"].Value, CultureInfo.InvariantCulture);
            }
            else if (Single.Match(line) is { Success: true } single)
            {
                var run = Number(single, "r");
                totals.LongestCallMs = Math.Max(totals.LongestCallMs, run);
                if (single.Groups["op"].Value.EndsWith(" slice", StringComparison.Ordinal))
                {
                    continue;
                }

                totals.QueueMs += Number(single, "q");
                totals.RunMs += run;
                totals.Calls++;
            }
            else if (Compiled.Match(line) is { Success: true } compiled)
            {
                totals.CompileMs = (totals.CompileMs ?? 0) + Number(compiled, "c");
                totals.CandidatesMs = (totals.CandidatesMs ?? 0) + Number(compiled, "k");
                var source = compiled.Groups["s"].Value;
                totals.CandidateSource = totals.CandidateSource is null || totals.CandidateSource == source ? source : "mixed";
            }
        }

        return totals;
    }

    private static double Number(Match match, string group)
    {
        return double.Parse(match.Groups[group].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
    }
}

[TestFixture]
public sealed class PerformanceTests : RecordedTest
{
    private const string RecordSwitch = "MODELS_CONTEXT_RECORD_PERF";
    private const double MemoryFloorMillion = 0.1;

    private static readonly List<PerfSample> Samples = [];
    private static readonly List<string> Problems = [];
    private static readonly PerfBudgets Budgets = CaseFiles.Read<PerfBudgets>("perf-budgets.json");

    private static TraceLines _lines = new();
    private static ILogger? _previous;
    private static Dictionary<string, double>? _baseline;
    private static bool _repeatRan;
    private static double _repeatGrowthMb;

    private static bool Recording => Environment.GetEnvironmentVariable(RecordSwitch) == "1";

    private static string Year => SampleModel.Application.Application.VersionNumber;

    private static string BaselinePath => Path.Combine(CaseFiles.Folder, "perf", Year + ".json");

    [OneTimeSetUp]
    public void Open()
    {
        _lines = new TraceLines();
        _previous = RevitProfiler.Logger;
        RevitProfiler.Use(_lines);
        Samples.Clear();
    }

    [OneTimeTearDown]
    public void Close()
    {
        RevitProfiler.Use(_previous ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        if (Samples.Count == 0)
        {
            return;
        }

        WriteReport();
        if (Recording)
        {
            WriteBaseline();
        }
    }

    [Test]
    public void CurrentModelOperations()
    {
        Record(Item("PF-01", "Snapshot, quick counts, scope ids, parameter catalog and value index of the HVAC model, cold, warm and after an edit"),
            () => Operations(SampleModel.Current, "Host"));
    }

    [Test]
    public void LinkOperations()
    {
        Record(Item("PF-02", "Snapshot, quick counts, scope ids, parameter catalog and value index of the Architectural link, cold, warm and after an edit"),
            () => Operations(SampleModel.Architectural, "Arch"));
    }

    [Test]
    public void FilterRuns()
    {
        Record(Item("PF-03", "Candidate ids, compile and match of representative filters, cold, warm and after an edit"), () =>
        {
            Begin();
            var adapter = new Adapter();
            foreach (var id in Budgets.Filters)
            {
                var item = CaseFiles.Filter(id);
                var documents = SampleModel.Of(item.Docs);
                var definition = Cases.Definition(item, (key, groups, text) => Pick(adapter, documents, key, groups, text));
                using var units = HostUnits.Set(item.HostLengthUnit);
                var scope = Cases.Scope(item);
                Phases($"{item.Docs}.filter.{id}", "filter", adapter, documents, item.HostLengthUnit is not null, () =>
                {
                    var count = adapter.Count(definition, documents, scope);
                    if (item.ExpectedCount is { } expected && count != expected)
                    {
                        Problems.Add($"{id}: matched {count}, expected {expected}.");
                    }

                    return new PerfResult(count);
                });
            }

            Finish();
        });
    }

    [Test]
    public void RepeatedRunKeepsMemory()
    {
        Record(Item("PF-04", $"The same filter run {Budgets.Repeat.Runs} times keeps managed memory flat"), () =>
        {
            Begin();
            var item = CaseFiles.Filter(Budgets.Repeat.Filter);
            var documents = SampleModel.Of(item.Docs);
            var adapter = new Adapter();
            var definition = Cases.Definition(item, (key, groups, text) => Pick(adapter, documents, key, groups, text));
            var scope = Cases.Scope(item);
            for (var warm = 0; warm < 3; warm++)
            {
                adapter.Count(definition, documents, scope);
            }

            var before = Megabytes();
            var times = new List<double>();
            for (var run = 0; run < Budgets.Repeat.Runs; run++)
            {
                var watch = Stopwatch.StartNew();
                adapter.Count(definition, documents, scope);
                times.Add(watch.Elapsed.TotalMilliseconds);
            }

            var after = Megabytes();
            times.Sort();
            _repeatGrowthMb = after - before;
            _repeatRan = true;
            var sample = Add(new PerfSample
            {
                Name = $"{item.Docs}.filter-repeat.{item.Id}",
                Phase = "warm",
                Ms = Math.Round(times[times.Count / 2], 1),
                Count = Budgets.Repeat.Runs,
                MemoryBeforeMb = Math.Round(before, 2),
                MemoryAfterMb = Math.Round(after, 2)
            }, "filter", warm: true);
            Evidence.Step($"{Budgets.Repeat.Runs} runs of {item.Id}: median {sample.Ms} ms, min {Math.Round(times[0], 1)} ms, max {Math.Round(times[^1], 1)} ms; managed memory {Math.Round(before, 2)} MB to {Math.Round(after, 2)} MB.");
            if (_repeatGrowthMb > Budgets.RepeatGrowthMb)
            {
                Problems.Add($"{sample.Name}: managed memory grew {Math.Round(_repeatGrowthMb, 2)} MB over {Budgets.Repeat.Runs} runs, budget {Budgets.RepeatGrowthMb} MB.");
            }

            Finish();
        });
    }

    private static CaseItem Item(string id, string description)
    {
        return new CaseItem
        {
            Id = id,
            Description = description,
            Expected = JsonSerializer.SerializeToElement("every timing within its budget and the 2026 baseline"),
            ManualSteps = ["Settings > Logging level Trace, use the panel on the sample, and read the 'Revit ...' lines for queue, run and max slice time; set the level back."]
        };
    }

    private static void Operations(SampleDocument document, string label)
    {
        Begin();
        var adapter = new Adapter();
        var documents = new[] { document };
        var source = document.Source;
        var key = document.Key;
        var none = CancellationToken.None;
        Phases($"{label}.snapshot", "snapshot", adapter, documents, false, () =>
        {
            var snapshot = Adapter.Run(adapter.Source.BuildSnapshotAsync(source, null, none));
            return new PerfResult(snapshot.ElementCount, snapshot);
        });
        Phases($"{label}.counts", "counts", adapter, documents, false,
            () => new PerfResult(Adapter.Run(adapter.Source.CountGroupsAsync(key, ScopeKind.WholeModel, none)).Sum(group => group.Count)));
        Phases($"{label}.scope", "scope", adapter, documents, false,
            () => new PerfResult(Adapter.Run(adapter.Source.CollectScopeAsync(key, ScopeKind.WholeModel, none)).Count));
        Phases($"{label}.catalog", "catalog", adapter, documents, false,
            () => new PerfResult(Adapter.Run(adapter.Source.GetParameterCatalogAsync(source, none)).Count));
        foreach (var target in Budgets.Values.Where(target => target.Docs == label))
        {
            var parameter = Keys.Of(target.Parameter);
            var category = Cases.Category(target.Category);
            Phases($"{label}.values.{target.Parameter}", "values", adapter, documents, false,
                () => new PerfResult(Adapter.Run(adapter.Evaluator.CountValuesAsync(source, parameter, [category], none)).Total));
        }

        Finish();
    }

    private static void Phases(string name, string operation, Adapter adapter, IReadOnlyList<SampleDocument> documents,
        bool editOpen, Func<PerfResult> work)
    {
        Drop(adapter, documents);
        Run(name, "cold", operation, work, true);
        Run(name, "warm", operation, work, false);
        if (editOpen)
        {
            Drop(adapter, documents);
            Run(name, "invalidated", operation, work, false);
            return;
        }

        using var transaction = new Transaction(SampleModel.Host, "Model Context perf edit");
        transaction.Start();
        try
        {
            var data = Areas.Edits.Setup;
            var duct = Oracle.Candidates(SampleModel.Host, [Cases.Category(data.Category)]).First();
            Oracle.Find(duct, Keys.Of(data.Parameter))!.Set(data.Value);
            Drop(adapter, documents);
            Run(name, "invalidated", operation, work, false);
        }
        finally
        {
            transaction.RollBack();
        }
    }

    internal static void Drop(Adapter adapter, IEnumerable<SampleDocument> documents)
    {
        foreach (var document in documents)
        {
            adapter.Cache.Invalidate(document.Source);
            adapter.Cache.InvalidateValues(document.Source);
        }

        adapter.Scopes.Clear();
    }

    private static PerfSample Run(string name, string phase, string operation, Func<PerfResult> work, bool memory)
    {
        var before = memory ? Megabytes() : 0;
        var mark = _lines.Lines.Count;
        var watch = Stopwatch.StartNew();
        var result = work();
        var elapsed = watch.Elapsed.TotalMilliseconds;
        var totals = TraceTotals.Of(_lines.Lines.Skip(mark));
        var sample = new PerfSample
        {
            Name = name,
            Phase = phase,
            Ms = Math.Round(elapsed, 1),
            QueueMs = Math.Round(totals.QueueMs, 1),
            RunMs = Math.Round(totals.RunMs, 1),
            LongestCallMs = Math.Round(totals.LongestCallMs, 1),
            Calls = totals.Calls,
            Count = result.Count,
            CompileMs = totals.CompileMs is { } compile ? Math.Round(compile, 1) : null,
            CandidatesMs = totals.CandidatesMs is { } candidates ? Math.Round(candidates, 1) : null,
            CandidateSource = totals.CandidateSource
        };
        if (memory)
        {
            var after = Megabytes();
            sample.MemoryBeforeMb = Math.Round(before, 2);
            sample.MemoryAfterMb = Math.Round(after, 2);
            if (result.Keep is DocumentSnapshot { ElementCount: > 0 } snapshot)
            {
                var million = snapshot.ElementCount / 1_000_000.0;
                sample.SnapshotMbPerMillion = Math.Round(snapshot.EstimatedBytes / 1048576.0 / million, 1);
                sample.MeasuredMbPerMillion = Math.Round((after - before) / million, 1);
                var judged = Math.Max(million, MemoryFloorMillion);
                if (snapshot.EstimatedBytes / 1048576.0 > Budgets.SnapshotMbPerMillion * judged)
                {
                    Problems.Add($"{name}: snapshot {snapshot.EstimatedBytes / 1048576.0:N1} MB for {snapshot.ElementCount} elements, budget {Budgets.SnapshotMbPerMillion} MB per million (at least {MemoryFloorMillion * 1_000_000:N0} elements).");
                }
            }
        }

        GC.KeepAlive(result);
        return Add(sample, operation, phase == "warm");
    }

    private static PerfSample Add(PerfSample sample, string operation, bool warm)
    {
        var budget = Budgets.Operations[operation];
        sample.BudgetMs = warm ? budget.WarmMs : budget.ColdMs;
        sample.Rating = Rating(sample.Ms);
        sample.Hitch = sample.LongestCallMs > Budgets.HitchMs;
        if (sample.Ms > sample.BudgetMs)
        {
            Problems.Add($"{sample.Key}: {sample.Ms} ms is over its budget of {sample.BudgetMs} ms.");
        }

        if (sample.LongestCallMs > Budgets.MaxCallMs)
        {
            Problems.Add($"{sample.Key}: one Revit call took {sample.LongestCallMs} ms, limit {Budgets.MaxCallMs} ms.");
        }

        if (!Recording && Baseline().TryGetValue(sample.Key, out var known))
        {
            sample.BaselineMs = known;
            if (sample.Ms > known * (1 + Budgets.RegressionPercent / 100) && sample.Ms - known > Budgets.RegressionMs)
            {
                Problems.Add($"{sample.Key}: {sample.Ms} ms against a baseline of {known} ms.");
            }
        }

        Samples.Add(sample);
        Evidence.Timing(sample.Key, (long)sample.Ms);
        Evidence.Step($"{sample.Key}: {sample.Ms} ms ({sample.Rating}), queue {sample.QueueMs} ms, run {sample.RunMs} ms, longest call {sample.LongestCallMs} ms{(sample.Hitch ? " (hitch)" : string.Empty)}, {sample.Count:N0} items.");
        return sample;
    }

    internal static string Rating(double ms)
    {
        return ms < 100 ? "Instant" : ms < 300 ? "Noticeable" : ms < 1000 ? "Slow" : "Annoying";
    }

    private static void Begin()
    {
        Problems.Clear();
        Evidence.Step(Recording
            ? $"Recording: the results replace the baseline {Path.GetFileName(BaselinePath)}."
            : File.Exists(BaselinePath) ? $"Comparing with the baseline {Path.GetFileName(BaselinePath)}." : "No baseline yet: reporting only.");
    }

    private static void Finish()
    {
        var own = Samples.Where(sample => sample.Hitch).Select(sample => sample.Key).ToList();
        Evidence.Values("within budget and baseline", Problems.Count == 0
            ? $"{Samples.Count} measurements; {own.Count} with a Revit call over {Budgets.HitchMs} ms"
            : $"{Problems.Count} problem(s)");
        Assert.That(Problems, Is.Empty, string.Join("\n", Problems));
    }

    private static NumberRange? Pick(Adapter adapter, IReadOnlyList<SampleDocument> documents, ParameterKey key,
        IReadOnlyList<long> groups, string text)
    {
        NumberRange? picked = null;
        foreach (var source in documents.Select(document => document.Source).Distinct())
        {
            var counts = Adapter.Run(adapter.Evaluator.CountValuesAsync(source, key, groups, CancellationToken.None));
            if (counts.Numbers is { } numbers && numbers.TryGetValue(text, out var range))
            {
                picked = picked is { } known ? known.Union(range) : range;
            }
        }

        return picked;
    }

    internal static double Megabytes()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(true) / 1048576.0;
    }

    private static Dictionary<string, double> Baseline()
    {
        if (_baseline is not null)
        {
            return _baseline;
        }

        _baseline = [];
        if (File.Exists(BaselinePath))
        {
            var file = JsonSerializer.Deserialize<BaselineFile>(File.ReadAllText(BaselinePath), CaseFiles.Options);
            _baseline = file?.Ms ?? [];
        }

        return _baseline;
    }

    private static void WriteReport()
    {
        var longest = Samples.OrderByDescending(sample => sample.LongestCallMs).First();
        var report = new
        {
            revit = Year,
            recorded = Recording,
            written = DateTime.Now,
            thresholds = new { instantMs = 100, noticeableMs = 300, slowMs = 1000, hitchMs = Budgets.HitchMs },
            longestCall = new { name = longest.Key, ms = longest.LongestCallMs },
            hitches = Samples.Where(sample => sample.Hitch).Select(sample => $"{sample.Key} {sample.LongestCallMs} ms").ToList(),
            repeatMemoryGrowthMb = _repeatRan ? Math.Round(_repeatGrowthMb, 2) : (double?)null,
            ratings = Samples.GroupBy(sample => sample.Rating).ToDictionary(group => group.Key, group => group.Count()),
            samples = Samples
        };
        File.WriteAllText(Path.Combine(Evidence.Folder, "perf.json"), JsonSerializer.Serialize(report, CaseFiles.Options));
    }

    private static void WriteBaseline()
    {
        var merged = new Dictionary<string, double>(Baseline(), StringComparer.Ordinal);
        foreach (var sample in Samples)
        {
            merged[sample.Key] = sample.Ms;
        }

        var file = new BaselineFile
        {
            Revit = Year,
            Recorded = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Ms = merged.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath)!);
        File.WriteAllText(BaselinePath, JsonSerializer.Serialize(file, CaseFiles.Options));
    }

    private sealed class BaselineFile
    {
        public string Revit { get; set; } = string.Empty;

        public string Recorded { get; set; } = string.Empty;

        public Dictionary<string, double> Ms { get; set; } = [];
    }
}
