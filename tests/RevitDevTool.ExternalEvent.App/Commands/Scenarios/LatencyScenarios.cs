#if DEBUG
using System.Diagnostics;
namespace RevitDevTool.ExternalEvent.App.Commands.Scenarios;

internal static class LatencyScenarios
{
    public static async Task RunSequentialLatency(
        IDispatchAdapter adapter, int requestCount, Action<string> log,
        BenchmarkReport report, CancellationTokenSource cts, WorkloadProfile profile = WorkloadProfile.NoOp)
    {
        var label = profile == WorkloadProfile.NoOp
            ? $"SequentialLatency: {adapter.Name}"
            : $"SequentialLatency [{profile}]: {adapter.Name}";
        log($"### {label} ({requestCount} requests)");
        log($"*{adapter.DispatchModel}*");

        await BenchmarkHelpers.Warmup(adapter);
        var timings = new RequestTiming[requestCount];
        var sw = Stopwatch.StartNew();
        var completed = 0;

        await Task.Run(async () =>
        {
            for (var i = 0; i < requestCount; i++)
            {
                if (cts.IsCancellationRequested) break;
                var idx = i;
                timings[idx].T0 = sw.ElapsedTicks;
                var task = adapter.RunAsync(app =>
                {
                    timings[idx].T2 = sw.ElapsedTicks;
                    ExecuteWorkload(app, profile);
                    timings[idx].T3 = sw.ElapsedTicks;
                    return idx;
                });
                timings[idx].T1 = sw.ElapsedTicks;
                try
                {
                    await task;
                    timings[idx].T4 = sw.ElapsedTicks;
                    completed++;
                }
                catch { /* counted as missing timing */ }
            }
        });

        sw.Stop();
        var valid = timings.Where(t => t.T4 > 0).ToArray();
        var stats = TimingStats.ComputeStats(valid);

        var categoryLabel = profile == WorkloadProfile.NoOp
            ? nameof(BenchmarkCategory.SequentialLatency)
            : $"{nameof(BenchmarkCategory.SequentialLatency)} [{profile}]";

        var result = new BenchmarkResult
        {
            AdapterName = adapter.Name,
            Category = categoryLabel,
            TotalRequested = requestCount,
            Completed = completed,
            WallTimeMs = sw.ElapsedMilliseconds,
            ThroughputRps = completed / sw.Elapsed.TotalSeconds,
            EnqueueLatency = stats.enqueue,
            WaitLatency = stats.wait,
            ExecutionDuration = stats.execution,
            TotalLatency = stats.total,
        };
        report.Results.Add(result);

        log(TimingStats.Summarize(label, valid));
        BenchmarkHelpers.LogResult(result, log);
        await BenchmarkHelpers.Cooldown();
    }

    private static void ExecuteWorkload(UIApplication app, WorkloadProfile profile)
    {
        switch (profile)
        {
            case WorkloadProfile.LightRevitRead:
                _ = app.Application.VersionNumber;
                break;

            case WorkloadProfile.TransactionRollback:
                ExecuteTransactionRollback(app);
                break;
        }
    }

    /// <summary>
    /// Real Revit workload: query a wall, read parameters, write via
    /// transaction, then rollback. Proves the dispatch correctly executes
    /// inside Revit API context with full DB access.
    /// </summary>
    private static void ExecuteTransactionRollback(UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document;
        if (doc is null) return;

        var wall = new FilteredElementCollector(doc)
            .OfClass(typeof(Wall))
            .WhereElementIsNotElementType()
            .FirstElement() as Wall;

        if (wall is null) return;

        var wallId = wall.Id;
        _ = wall.WallType?.Name;
        _ = wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();
        _ = wall.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();

        using var tx = new Transaction(doc, "BenchmarkRollback");
        tx.Start();
        try
        {
            var target = doc.GetElement(wallId) as Wall;
            target?.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)
                  ?.Set($"bench-{DateTime.UtcNow.Ticks}");
        }
        finally
        {
            tx.RollBack();
        }
    }
}
#endif
