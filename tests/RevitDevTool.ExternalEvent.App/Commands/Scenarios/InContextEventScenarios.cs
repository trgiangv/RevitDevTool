#if DEBUG
using System.Diagnostics;
namespace RevitDevTool.ExternalEvent.App.Commands.Scenarios;

/// <summary>
/// Tests fixed-handler event dispatch overhead from normal UI context.
/// Measures Raise → handler execution latency without per-call delegate injection.
/// Only runs against <see cref="IInContextEventAdapter"/> (pre-registered handler reuse).
/// </summary>
internal static class InContextEventScenarios
{
    private const int ScenarioTimeoutMs = 120_000;

    public static async Task RunSequentialRaiseLatency(
        IInContextEventAdapter adapter, int requestCount,
        Action<string> log, BenchmarkReport report, CancellationTokenSource cts)
    {
        var label = $"SequentialRaise: {adapter.Name}";
        log($"### {label} ({requestCount} requests)");
        log("");
        log($"*{adapter.DispatchModel}*");
        log("");

        await BenchmarkHelpers.Warmup(adapter);
        var enqueue = new double[requestCount];
        var total = new double[requestCount];
        var sw = Stopwatch.StartNew();
        var completed = 0;

        await Task.Run(async () =>
        {
            for (var i = 0; i < requestCount; i++)
            {
                if (cts.IsCancellationRequested) break;
                var t0 = sw.ElapsedTicks;
                try
                {
                    var task = adapter.RaiseAndWaitAsync(cts.Token);
                    var t1 = sw.ElapsedTicks;
                    await task;
                    var t4 = sw.ElapsedTicks;

                    enqueue[i] = TicksToUs(t1 - t0);
                    total[i] = TicksToUs(t4 - t0);
                    completed++;
                }
                catch { /* counted as missing */ }
            }
        });

        sw.Stop();
        var validEnqueue = enqueue.Take(completed).OrderBy(x => x).ToArray();
        var validTotal = total.Take(completed).OrderBy(x => x).ToArray();

        var result = new BenchmarkResult
        {
            Suite = BenchmarkSuite.InContextEventReuse,
            AdapterName = adapter.Name,
            Category = nameof(BenchmarkCategory.SequentialRaise),
            TotalRequested = requestCount,
            Completed = completed,
            WallTimeMs = sw.ElapsedMilliseconds,
            ThroughputRps = completed / sw.Elapsed.TotalSeconds,
            EnqueueLatency = PercentileStats.FromSorted(validEnqueue),
            TotalLatency = PercentileStats.FromSorted(validTotal),
        };
        report.Results.Add(result);

        log($"**{label}** ({completed} requests)");
        log("");
        log("| Phase | mean | p50 | p95 | p99 | max |");
        log("|-------|------|-----|-----|-----|-----|");
        log($"| Enqueue | {result.EnqueueLatency!.Mean:F1}μs | {result.EnqueueLatency.Median:F1}μs | {result.EnqueueLatency.P95:F1}μs | {result.EnqueueLatency.P99:F1}μs | {result.EnqueueLatency.Max:F1}μs |");
        log($"| Total | {result.TotalLatency!.Mean:F1}μs | {result.TotalLatency.Median:F1}μs | {result.TotalLatency.P95:F1}μs | {result.TotalLatency.P99:F1}μs | {result.TotalLatency.Max:F1}μs |");
        log("");
        BenchmarkHelpers.LogResult(result, log);
        await BenchmarkHelpers.Cooldown();
    }

    public static async Task RunDirectInvocation(
        IInContextEventAdapter adapter, int requestCount,
        Action<string> log, BenchmarkReport report, CancellationTokenSource cts)
    {
        log($"### DirectInvocation: {adapter.Name} ({requestCount} requests)");
        log("");

        if (!adapter.SupportsDirectInvocation)
        {
            log("**SKIPPED** — adapter does not support direct invocation.");
            log("");
            report.Results.Add(new BenchmarkResult
            {
                Suite = BenchmarkSuite.InContextEventReuse,
                AdapterName = adapter.Name,
                Category = "DirectInvocation",
                Notes = "Not supported",
            });
            log("");
            return;
        }

        log("*Raising from Revit API context — should execute inline.*");
        log("");
        await BenchmarkHelpers.Warmup(adapter);
        var c = new BenchmarkCounters();
        var totalSw = Stopwatch.StartNew();

        for (var i = 0; i < requestCount; i++)
        {
            if (cts.IsCancellationRequested) break;
            await c.RunGuarded(() => adapter.RaiseAndWaitAsync(cts.Token));
        }

        totalSw.Stop();
        var result = c.ToResult(adapter.Name, BenchmarkSuite.InContextEventReuse, BenchmarkCategory.DirectInvocation, requestCount, totalSw.ElapsedMilliseconds);
        result.Notes = "Raised from Revit API context (IExternalCommand.Execute thread)";
        report.Results.Add(result);
        BenchmarkHelpers.LogResult(result, log);
        await BenchmarkHelpers.Cooldown();
    }

    public static async Task RunConcurrentRaise(
        IInContextEventAdapter adapter, int requestCount, int threadCount,
        Action<string> log, BenchmarkReport report, CancellationTokenSource cts)
    {
        log($"### ConcurrentRaise: {adapter.Name} ({requestCount} req from {threadCount} threads)");
        log("");
        log("*Fire N raises concurrently from background, await all.*");
        log("");

        await BenchmarkHelpers.Warmup(adapter);
        var c = new BenchmarkCounters();
        var barrier = new Barrier(threadCount);
        var distribution = BenchmarkHelpers.DistributeWork(requestCount, threadCount);
        var totalSw = Stopwatch.StartNew();

        var threads = new List<Task>(threadCount);
        for (var t = 0; t < threadCount; t++)
        {
            var count = distribution[t];
            threads.Add(Task.Run(async () =>
            {
                barrier.SignalAndWait();
                var batch = new List<Task>(count);
                for (var i = 0; i < count; i++)
                    batch.Add(adapter.RaiseAndWaitAsync(cts.Token));
                foreach (var task in batch)
                    await c.AwaitAndRecord(task);
            }));
        }

        var allDone = Task.WhenAll(threads);
        var winner = await Task.WhenAny(allDone, Task.Delay(ScenarioTimeoutMs));
        totalSw.Stop();

        var timedOut = winner != allDone;
        var result = c.ToResult(adapter.Name, BenchmarkSuite.InContextEventReuse, BenchmarkCategory.ConcurrentRaise, requestCount, totalSw.ElapsedMilliseconds);
        if (timedOut)
            result.Notes = $"Scenario timed out after {ScenarioTimeoutMs / 1000}s";
        report.Results.Add(result);

        log($"- **Dispatched:** {c.Completed}, **TimedOut:** {c.TimedOut}, **Faulted:** {c.Faulted}");
        if (timedOut) log($"- ⚠ **WARNING:** scenario timed out after {ScenarioTimeoutMs / 1000}s");
        BenchmarkHelpers.LogResult(result, log);
        await BenchmarkHelpers.Cooldown();
    }

    private static double TicksToUs(long ticks)
    {
        return ticks * 1_000_000.0 / Stopwatch.Frequency;
    }
}
#endif
