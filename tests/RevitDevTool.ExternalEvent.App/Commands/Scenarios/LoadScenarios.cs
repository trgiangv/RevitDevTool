#if DEBUG
using System.Diagnostics;
namespace RevitDevTool.ExternalEvent.App.Commands.Scenarios;

internal static class LoadScenarios
{
    private const int ScenarioTimeoutMs = 120_000;

    public static async Task RunProducerSequential(
        IDispatchAdapter adapter, int requestCount, int producerCount,
        Action<string> log, BenchmarkReport report, CancellationTokenSource cts)
    {
        log($"### ProducerSequential: {adapter.Name} ({requestCount} req, {producerCount} producers)");
        log("");
        log("*Each producer awaits one request before submitting the next.*");
        log("");

        await BenchmarkHelpers.Warmup(adapter);
        var c = new BenchmarkCounters();
        var distribution = BenchmarkHelpers.DistributeWork(requestCount, producerCount);
        var totalSw = Stopwatch.StartNew();

        var scenarioCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        scenarioCts.CancelAfter(ScenarioTimeoutMs);
        bool scenarioTimedOut;
        try
        {
            var tasks = new Task[producerCount];
            for (var p = 0; p < producerCount; p++)
                tasks[p] = RunProducer(adapter, distribution[p], c, scenarioCts.Token);

            await Task.WhenAll(tasks);
            scenarioTimedOut = scenarioCts.IsCancellationRequested && !cts.IsCancellationRequested;
        }
        finally { scenarioCts.Dispose(); }

        totalSw.Stop();

        var result = c.ToResult(adapter.Name, BenchmarkCategory.ProducerSequential, requestCount, totalSw.ElapsedMilliseconds);
        if (scenarioTimedOut)
            result.Notes = $"Scenario timed out after {ScenarioTimeoutMs / 1000}s";
        report.Results.Add(result);
        BenchmarkHelpers.LogResult(result, log);
        await BenchmarkHelpers.Cooldown();
    }

    private static Task RunProducer(IDispatchAdapter adapter, int count, BenchmarkCounters c, CancellationToken token)
    {
        return Task.Run(async () =>
        {
            for (var i = 0; i < count; i++)
            {
                if (token.IsCancellationRequested) break;
                await c.RunGuarded(() => adapter.RunAsync(_ => { }, token));
            }
        }, token);
    }

    public static async Task RunTrueBurst(
        IDispatchAdapter adapter, int requestCount, int threadCount,
        Action<string> log, BenchmarkReport report, CancellationTokenSource cts)
    {
        log($"### TrueBurst: {adapter.Name} ({requestCount} req from {threadCount} threads)");
        log("");
        log("*All requests enqueued first, then all awaited.*");
        log("");

        await BenchmarkHelpers.Warmup(adapter);
        var executed = new int[1];
        var c = new BenchmarkCounters();
        var barrier = new Barrier(threadCount);
        var distribution = BenchmarkHelpers.DistributeWork(requestCount, threadCount);
        var totalSw = Stopwatch.StartNew();

        var threads = new Task[threadCount];
        for (var t = 0; t < threadCount; t++)
            threads[t] = RunBurstThread(adapter, distribution[t], c, barrier, executed);

        var allDone = Task.WhenAll(threads);
        var winner = await Task.WhenAny(allDone, Task.Delay(ScenarioTimeoutMs));
        totalSw.Stop();

        var timedOut = winner != allDone;
        var executedCount = executed[0];

        var result = c.ToResult(adapter.Name, BenchmarkCategory.TrueBurst, requestCount, totalSw.ElapsedMilliseconds);
        result.Notes = timedOut
            ? $"Scenario timed out after {ScenarioTimeoutMs / 1000}s — executed {executedCount}/{requestCount}"
            : $"Actually executed callback: {executedCount}/{requestCount}";
        report.Results.Add(result);

        log($"- **Requested:** {requestCount}, **Executed:** {executedCount}, **TimedOut:** {c.TimedOut}");
        if (timedOut) log($"- ⚠ **WARNING:** scenario timed out after {ScenarioTimeoutMs / 1000}s");
        BenchmarkHelpers.LogResult(result, log);
        await BenchmarkHelpers.Cooldown();
    }

    private static Task RunBurstThread(
        IDispatchAdapter adapter, int count, BenchmarkCounters c, Barrier barrier, int[] executed)
    {
        return Task.Run(async () =>
        {
            barrier.SignalAndWait();
            var batch = new List<Task<int>>(count);
            for (var i = 0; i < count; i++)
                batch.Add(adapter.RunAsync(_ => { Interlocked.Increment(ref executed[0]); return 1; }));
            foreach (var task in batch)
                await c.AwaitAndRecord(task);
        });
    }

    public static async Task RunSustainedLoad(
        IDispatchAdapter adapter, int durationSeconds, int producerCount, int maxInFlight,
        Action<string> log, BenchmarkReport report, CancellationTokenSource cts)
    {
        log($"### SustainedLoad: {adapter.Name} ({durationSeconds}s, {producerCount} producers, max in-flight={maxInFlight})");
        log("");

        await BenchmarkHelpers.Warmup(adapter);
        var c = new BenchmarkCounters();
        var throttle = new SemaphoreSlim(maxInFlight, maxInFlight);
        var deadline = TimeSpan.FromSeconds(durationSeconds);
        var totalSw = Stopwatch.StartNew();

        var scenarioCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        scenarioCts.CancelAfter(durationSeconds * 1000 + 30_000);
        try
        {
            var tasks = new Task[producerCount];
            for (var p = 0; p < producerCount; p++)
                tasks[p] = RunSustainedProducer(adapter, c, throttle, totalSw, deadline, scenarioCts.Token);

            await Task.WhenAll(tasks);
        }
        finally { scenarioCts.Dispose(); }

        totalSw.Stop();

        var result = c.ToResult(adapter.Name, BenchmarkCategory.SustainedLoad, c.Total, totalSw.ElapsedMilliseconds);
        report.Results.Add(result);
        log($"- **Sustained RPS:** {result.ThroughputRps:F0}");
        BenchmarkHelpers.LogResult(result, log);
        await BenchmarkHelpers.Cooldown();
    }

    private static Task RunSustainedProducer(
        IDispatchAdapter adapter, BenchmarkCounters c, SemaphoreSlim throttle,
        Stopwatch clock, TimeSpan deadline, CancellationToken token)
    {
        return Task.Run(async () =>
        {
            while (clock.Elapsed < deadline && !token.IsCancellationRequested)
            {
                try { await throttle.WaitAsync(token); }
                catch (OperationCanceledException) { break; }

                try { await c.RunGuarded(() => adapter.RunAsync(_ => { }, token)); }
                finally { throttle.Release(); }
            }
        }, token);
    }
}
#endif
