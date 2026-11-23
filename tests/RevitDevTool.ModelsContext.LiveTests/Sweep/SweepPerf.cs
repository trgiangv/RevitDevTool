using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Scope;
using System.Diagnostics;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed class SweepPerf(TraceLines lines)
{
    public const double HitchMs = 50;
    public const double MemoryFloorMillion = 0.1;

    private static readonly CancellationToken None = CancellationToken.None;

    public List<PerfSample> Samples { get; } = [];

    public void Operations(SampleDocument document, string label, Element? editable)
    {
        var adapter = new Adapter();
        var documents = new[] { document };
        var source = document.Source;
        var key = document.Key;
        Phases(label + ".snapshot", adapter, documents, editable, () =>
        {
            var snapshot = Adapter.Run(adapter.Source.BuildSnapshotAsync(source, null, None));
            return new PerfResult(snapshot.ElementCount, snapshot);
        });
        var groups = Adapter.Run(adapter.Source.CountGroupsAsync(key, ScopeKind.WholeModel, None));
        Phases(label + ".counts", adapter, documents, editable,
            () => new PerfResult(Adapter.Run(adapter.Source.CountGroupsAsync(key, ScopeKind.WholeModel, None)).Sum(group => group.Count)));
        Phases(label + ".catalog", adapter, documents, editable,
            () => new PerfResult(Adapter.Run(adapter.Source.GetParameterCatalogAsync(source, None)).Count));

        var filled = groups.Where(group => group.Count > 0).OrderByDescending(group => group.Count).ToList();
        if (filled.Count == 0)
        {
            Evidence.Step($"{label}: no category has elements; value list and filter skipped.");
            return;
        }

        var largest = filled[0];
        var category = largest.GroupId;
        var catalog = Picks.Catalog(adapter, document).Where(parameter => parameter.Categories.Contains(category)).ToList();
        var parameter = catalog.FirstOrDefault(candidate => candidate.Kind == ValueKind.Text) ?? catalog.FirstOrDefault();
        if (parameter is null)
        {
            Evidence.Step($"{label}: the largest category has no catalog parameter; value list and filter skipped.");
            return;
        }

        Evidence.Step($"{label}: value list and filter on '{parameter.DisplayName}' of {ActiveModel.CategoryName(document.Document, category)} ({largest.Count:N0} elements).");
        Phases(label + ".values", adapter, documents, editable,
            () => new PerfResult(Adapter.Run(adapter.Evaluator.CountValuesAsync(source, parameter.Key, [category], None)).Total));
        var definition = new FilterDefinition([category], FilterSet.All(new RuleNode(parameter.Key, FilterOperator.NotEmpty)));
        Phases(label + ".filter", adapter, documents, editable,
            () => new PerfResult(Adapter.Run(adapter.Evaluator.EvaluateAsync(key, definition, null, None)).Count));
    }

    private void Phases(string name, Adapter adapter, IReadOnlyList<SampleDocument> documents, Element? editable, Func<PerfResult> work)
    {
        PerformanceTests.Drop(adapter, documents);
        Run(name, "cold", work, true);
        Run(name, "warm", work, false);
        if (editable is null)
        {
            PerformanceTests.Drop(adapter, documents);
            Run(name, "invalidated", work, false);
            return;
        }

        using var transaction = new Transaction(editable.Document, "Model Context sweep perf edit");
        transaction.Start();
        try
        {
            editable.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set("mc-sweep-perf");
            PerformanceTests.Drop(adapter, documents);
            Run(name, "invalidated", work, false);
        }
        finally
        {
            transaction.RollBack();
        }
    }

    private void Run(string name, string phase, Func<PerfResult> work, bool memory)
    {
        var before = memory ? PerformanceTests.Megabytes() : 0;
        var mark = lines.Lines.Count;
        var watch = Stopwatch.StartNew();
        var result = work();
        var elapsed = watch.Elapsed.TotalMilliseconds;
        var totals = TraceTotals.Of(lines.Lines.Skip(mark));
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
            Rating = PerformanceTests.Rating(elapsed),
            Hitch = totals.LongestCallMs > HitchMs
        };
        if (memory)
        {
            var after = PerformanceTests.Megabytes();
            sample.MemoryBeforeMb = Math.Round(before, 2);
            sample.MemoryAfterMb = Math.Round(after, 2);
            if (result.Keep is DocumentSnapshot { ElementCount: > 0 } snapshot)
            {
                var million = Math.Max(snapshot.ElementCount / 1_000_000.0, MemoryFloorMillion);
                sample.SnapshotMbPerMillion = Math.Round(snapshot.EstimatedBytes / 1048576.0 / million, 1);
                sample.MeasuredMbPerMillion = Math.Round((after - before) / million, 1);
            }
        }

        GC.KeepAlive(result);
        Samples.Add(sample);
        Evidence.Timing(sample.Key, (long)sample.Ms);
        Evidence.Step($"{sample.Key}: {sample.Ms} ms ({sample.Rating}), longest call {sample.LongestCallMs} ms{(sample.Hitch ? " (hitch)" : string.Empty)}, {sample.Count:N0} items.");
    }
}
