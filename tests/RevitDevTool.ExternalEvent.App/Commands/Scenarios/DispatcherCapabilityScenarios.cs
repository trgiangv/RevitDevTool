#if DEBUG
using System.Collections.Concurrent;
using System.Diagnostics;
namespace RevitDevTool.ExternalEvent.App.Commands.Scenarios;

/// <summary>
/// Tests dispatcher queue capabilities: FIFO ordering, cancellation semantics,
/// error propagation fidelity, re-entrant dispatch, GC pressure.
/// Only runs against <see cref="IDispatchAdapter"/> (arbitrary delegate execution).
/// </summary>
internal static class DispatcherCapabilityScenarios
{
    public static async Task RunDirectInvocation(
        IDispatchAdapter adapter, int requestCount,
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
                AdapterName = adapter.Name,
                Category = nameof(BenchmarkCategory.DirectInvocation),
                Notes = "Not supported",
            });
            log("");
            return;
        }

        log("*Running from Revit API context — should execute inline.*");
        log("");
        await BenchmarkHelpers.Warmup(adapter);
        var c = new BenchmarkCounters();
        var totalSw = Stopwatch.StartNew();

        for (var i = 0; i < requestCount; i++)
        {
            if (cts.IsCancellationRequested) break;
            await c.RunGuarded(() => adapter.RunAsync(_ => { }));
        }

        totalSw.Stop();
        var result = c.ToResult(adapter.Name, BenchmarkCategory.DirectInvocation, requestCount, totalSw.ElapsedMilliseconds);
        result.Notes = "Invoked from Revit API context (IExternalCommand.Execute thread)";
        report.Results.Add(result);
        BenchmarkHelpers.LogResult(result, log);
        await BenchmarkHelpers.Cooldown();
    }

    public static async Task RunNestedReentry(
        IDispatchAdapter adapter, int depth,
        Action<string> log, BenchmarkReport report, CancellationTokenSource cts)
    {
        log($"### NestedReentry: {adapter.Name} (depth={depth})");
        log("");
        log("*Sequential re-entrant dispatch: await RunAsync, then dispatch again.*");
        log("");

        await BenchmarkHelpers.Warmup(adapter);
        var actualDepth = 0;
        var faulted = false;
        string? error = null;

        var sw = Stopwatch.StartNew();
        try
        {
            await Task.Run(async () =>
            {
                for (var i = 0; i < depth; i++)
                {
                    await adapter.RunAsync(_ =>
                    {
                        Interlocked.Increment(ref actualDepth);
                    });
                }
            });
        }
        catch (Exception ex) { faulted = true; error = ex.GetType().Name; }

        sw.Stop();
        report.Results.Add(new BenchmarkResult
        {
            AdapterName = adapter.Name,
            Category = nameof(BenchmarkCategory.NestedReentry),
            TotalRequested = depth,
            Completed = actualDepth,
            Faulted = faulted ? 1 : 0,
            WallTimeMs = sw.ElapsedMilliseconds,
            Notes = faulted ? $"Faulted with {error}" : null,
        });

        log($"- **Depth reached:** {actualDepth}/{depth}, **Faulted:** {faulted}{(error != null ? $" ({error})" : "")}");
        log($"- **Wall time:** {sw.ElapsedMilliseconds}ms");
        log("");
        await BenchmarkHelpers.Cooldown();
    }

    #region Cancellation

    public static async Task RunCancellationLifecycle(
        IDispatchAdapter adapter, int requestCount,
        Action<string> log, BenchmarkReport report, CancellationTokenSource cts)
    {
        log($"### CancellationLifecycle: {adapter.Name}");
        log("");

        if (!adapter.SupportsCancellation)
        {
            log("**Cancellation not supported** — reported as capability difference, not failure.");
            log("");
            report.Results.Add(new BenchmarkResult
            {
                AdapterName = adapter.Name,
                Category = nameof(BenchmarkCategory.CancellationLifecycle),
                Notes = "Cancellation not supported by this adapter",
            });
            log("");
            return;
        }

        log($"1. Pre-cancelled token ({requestCount} req)...");
        var preCancelledOk = await TestPreCancelled(adapter, requestCount);

        log($"2. Cancel after enqueue ({requestCount} req)...");
        var afterEnqueueOk = await TestCancelAfterEnqueue(adapter, requestCount);

        log($"3. Cancel during callback...");
        var duringCallbackOk = await TestCancelDuringCallback(adapter);

        report.Results.Add(new BenchmarkResult
        {
            AdapterName = adapter.Name,
            Category = nameof(BenchmarkCategory.CancellationLifecycle),
            TotalRequested = requestCount * 2 + 1,
            Completed = preCancelledOk + afterEnqueueOk + duringCallbackOk,
            Notes = $"Pre-cancelled: {preCancelledOk}/{requestCount}, " +
                    $"After-enqueue: {afterEnqueueOk}/{requestCount}, " +
                    $"During-callback: {duringCallbackOk}/1",
        });

        log($"- **Pre-cancelled:** {preCancelledOk}/{requestCount}");
        log($"- **After-enqueue:** {afterEnqueueOk}/{requestCount}");
        log($"- **During-callback:** {duringCallbackOk}/1");
        log("");
        await BenchmarkHelpers.Cooldown();
    }

    private static async Task<int> TestPreCancelled(IDispatchAdapter adapter, int count)
    {
        var ok = 0;
        using var preCancel = new CancellationTokenSource();
        await preCancel.CancelAsync();
        for (var i = 0; i < count; i++)
        {
            try { await adapter.RunAsync(_ => { }, preCancel.Token); }
            catch (OperationCanceledException) { ok++; }
            catch { /* other */ }
        }
        return ok;
    }

    private static async Task<int> TestCancelAfterEnqueue(IDispatchAdapter adapter, int count)
    {
        var ok = 0;
        using var cts = new CancellationTokenSource();
        var batch = new List<Task>(count);

        await Task.Run(async () =>
        {
            for (var i = 0; i < count; i++)
                batch.Add(adapter.RunAsync(_ => { Thread.SpinWait(100); }, cts.Token));
            await cts.CancelAsync();
        }, cts.Token);

        foreach (var task in batch)
        {
            try { await task; }
            catch (OperationCanceledException) { ok++; }
            catch { /* timeout or other */ }
        }
        return ok;
    }

    private static async Task<int> TestCancelDuringCallback(IDispatchAdapter adapter)
    {
        using var cts = new CancellationTokenSource();
        var callbackStarted = new ManualResetEventSlim(false);
        try
        {
            var longTask = adapter.RunAsync(_ =>
            {
                callbackStarted.Set();
                Thread.Sleep(2000);
            }, cts.Token);

            if (callbackStarted.Wait(5000))
                await cts.CancelAsync();

            try { await longTask; }
            catch (OperationCanceledException) { return 1; }
            catch { /* may complete normally */ }
        }
        catch (OperationCanceledException) { return 1; }
        catch { /* ignore */ }
        return 0;
    }

    #endregion

    #region Error Propagation

    public static async Task RunErrorPropagation(
        IDispatchAdapter adapter, int requestCount,
        Action<string> log, BenchmarkReport report, CancellationTokenSource cts)
    {
        log($"### ErrorPropagation: {adapter.Name} ({requestCount} requests)");
        log("");
        await BenchmarkHelpers.Warmup(adapter);

        var (propagated, swallowed, wrongType, timedOut) = await CollectErrorResults(adapter, requestCount);

        report.Results.Add(new BenchmarkResult
        {
            AdapterName = adapter.Name,
            Category = nameof(BenchmarkCategory.ErrorPropagation),
            TotalRequested = requestCount,
            Completed = propagated,
            Faulted = wrongType,
            TimedOut = timedOut,
            Notes = $"Propagated: {propagated}, Swallowed: {swallowed}, WrongType: {wrongType}",
        });

        log($"- **Propagated:** {propagated}, **Swallowed:** {swallowed}, **WrongType:** {wrongType}, **TimedOut:** {timedOut}");
        log($"- **Error fidelity:** {(propagated == requestCount ? "PASS" : "FAIL")}");
        log("");
        await BenchmarkHelpers.Cooldown();
    }

    private static async Task<(int propagated, int swallowed, int wrongType, int timedOut)>
        CollectErrorResults(IDispatchAdapter adapter, int requestCount)
    {
        var tasks = await Task.Run(() =>
        {
            var batch = new List<Task<int>>(requestCount);
            for (var i = 0; i < requestCount; i++)
            {
                var idx = i;
                batch.Add(adapter.RunAsync<int>(_ =>
                    throw new InvalidOperationException($"Test error #{idx}")));
            }
            return batch;
        });

        int propagated = 0, swallowed = 0, wrongType = 0, timedOut = 0;
        foreach (var task in tasks)
        {
            switch (await ClassifyErrorTask(task))
            {
                case ErrorOutcome.Propagated: propagated++; break;
                case ErrorOutcome.Swallowed:  swallowed++;  break;
                case ErrorOutcome.TimedOut:   timedOut++;    break;
                case ErrorOutcome.WrongType:  wrongType++;   break;
            }
        }
        return (propagated, swallowed, wrongType, timedOut);
    }

    private enum ErrorOutcome { Propagated, Swallowed, WrongType, TimedOut }

    private static async Task<ErrorOutcome> ClassifyErrorTask(Task task)
    {
        try { await task; return ErrorOutcome.Swallowed; }
        catch (InvalidOperationException) { return ErrorOutcome.Propagated; }
        catch (TimeoutException) { return ErrorOutcome.TimedOut; }
        catch { return ErrorOutcome.WrongType; }
    }

    #endregion

    public static async Task RunFifoOrder(
        IDispatchAdapter adapter, int requestCount,
        Action<string> log, CancellationTokenSource cts)
    {
        log($"### FIFO Order: {adapter.Name} ({requestCount} requests)");
        log("");

        await BenchmarkHelpers.Warmup(adapter);
        var executionOrder = new ConcurrentQueue<int>();
        await Task.Run(async () =>
        {
            var tasks = new List<Task>(requestCount);
            for (var i = 0; i < requestCount; i++)
            {
                var idx = i;
                tasks.Add(adapter.RunAsync(_ => { executionOrder.Enqueue(idx); }, cts.Token));
            }
            await Task.WhenAll(tasks);
        });

        var order = executionOrder.ToArray();
        if (order.Length == 0)
        {
            log($"**SKIPPED** — no callbacks executed (0/{requestCount})");
            log("");
            await BenchmarkHelpers.Cooldown();
            return;
        }

        var outOfOrder = 0;
        for (var i = 1; i < order.Length; i++)
            if (order[i] < order[i - 1]) outOfOrder++;

        log($"- **Total:** {order.Length}, **Out of order:** {outOfOrder}");
        log($"- **FIFO:** {(outOfOrder == 0 ? "PASS" : "FAIL")}");
        log("");
        await BenchmarkHelpers.Cooldown();
    }

}
#endif
