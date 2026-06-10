#if DEBUG
using System.Collections.Concurrent;
using System.Diagnostics;
namespace RevitDevTool.ExternalEvent.App.Commands;

internal sealed class StressTestRunner(Action<string> log)
{
    private CancellationTokenSource? _cts;

    public void Cancel()
    {
        _cts?.Cancel();
    }

    private sealed class Counters
    {
        private int _completed, _failed, _timedOut, _cancelled;

        public int Completed => _completed;
        public int Failed => _failed;
        public int TimedOut => _timedOut;
        public int Cancelled => _cancelled;
        public int Total => _completed + _failed + _timedOut + _cancelled;

        public void RecordSuccess() => Interlocked.Increment(ref _completed);
        public void RecordTimeout() => Interlocked.Increment(ref _timedOut);
        public void RecordFailure() => Interlocked.Increment(ref _failed);
        public void RecordCancel() => Interlocked.Increment(ref _cancelled);

        public async Task AwaitAndRecord(Task task, int timeoutMs = 30_000)
        {
            try
            {
                var winner = await Task.WhenAny(task, Task.Delay(timeoutMs));
                if (winner != task) { RecordTimeout(); return; }
                await task;
                RecordSuccess();
            }
            catch (OperationCanceledException) { RecordCancel(); }
            catch (TimeoutException) { RecordTimeout(); }
            catch { RecordFailure(); }
        }

        public async Task RunGuarded(Func<Task> action)
        {
            try
            {
                await action();
                RecordSuccess();
            }
            catch (TimeoutException) { RecordTimeout(); }
            catch (OperationCanceledException) { RecordCancel(); }
            catch { RecordFailure(); }
        }
    }

    private async Task RunParallel(int totalCount, int producerCount, Func<CancellationToken, Func<Task>, Task> body)
    {
        var perProducer = totalCount / producerCount;
        var tasks = new List<Task>(producerCount);
        for (var p = 0; p < producerCount; p++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (var i = 0; i < perProducer; i++)
                {
                    if (_cts!.IsCancellationRequested) break;
                    await body(_cts.Token, () => Task.CompletedTask);
                }
            }));
        }
        await Task.WhenAll(tasks);
    }

    public async Task RunOverheadProfile(IDispatchAdapter adapter, int requestCount)
    {
        _cts = new CancellationTokenSource();
        log($"=== Overhead Profile: {adapter.Name} ({requestCount} requests) ===");

        var timings = new RequestTiming[requestCount];
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < requestCount; i++)
        {
            if (_cts.IsCancellationRequested) break;
            var idx = i;
            await Task.Run(async () =>
            {
                timings[idx].T0 = sw.ElapsedTicks;
                var task = adapter.RunAsync(app =>
                {
                    timings[idx].T2 = sw.ElapsedTicks;
                    Thread.SpinWait(10);
                    timings[idx].T3 = sw.ElapsedTicks;
                    return idx;
                });
                timings[idx].T1 = sw.ElapsedTicks;
                await task;
                timings[idx].T4 = sw.ElapsedTicks;
            });
        }

        var valid = timings.Where(t => t.T4 > 0).ToArray();
        log(TimingStats.Summarize(adapter.Name, valid));
        log("");
    }

    public async Task RunThroughput(IDispatchAdapter adapter, int requestCount, int producerCount)
    {
        _cts = new CancellationTokenSource();
        log($"=== Throughput: {adapter.Name} ({requestCount} req, {producerCount} producers) ===");

        var c = new Counters();
        var totalSw = Stopwatch.StartNew();

        await RunParallel(requestCount, producerCount, async (token, _) =>
            await c.RunGuarded(() => adapter.RunAsync(_ => { }, token)));

        totalSw.Stop();
        log($"  Completed: {c.Completed}, Failed: {c.Failed}, Hung(timeout): {c.TimedOut}");
        log($"  Wall time: {totalSw.ElapsedMilliseconds}ms, Throughput: {c.Completed / totalSw.Elapsed.TotalSeconds:F0} req/s");
        log("");
    }

    public async Task RunBurst(IDispatchAdapter adapter, int requestCount, int threadCount)
    {
        _cts = new CancellationTokenSource();
        log($"=== Burst: {adapter.Name} ({requestCount} req from {threadCount} threads) ===");

        var executed = 0;
        var c = new Counters();
        var barrier = new Barrier(threadCount);
        var perThread = requestCount / threadCount;
        var totalSw = Stopwatch.StartNew();

        var threads = new List<Task>(threadCount);
        for (var t = 0; t < threadCount; t++)
        {
            threads.Add(Task.Run(async () =>
            {
                barrier.SignalAndWait();
                var batch = new List<Task<int>>(perThread);
                for (var i = 0; i < perThread; i++)
                    batch.Add(adapter.RunAsync(app => { Interlocked.Increment(ref executed); return 1; }));
                foreach (var task in batch)
                    await c.AwaitAndRecord(task);
            }));
        }

        await Task.WhenAll(threads);
        totalSw.Stop();

        log($"  Expected: {requestCount}, Executed: {executed}, Lost: {requestCount - executed}");
        log($"  Hung(timeout): {c.TimedOut}, Faulted: {c.Failed}");
        log($"  Wall time: {totalSw.ElapsedMilliseconds}ms");
        log("");
    }

    public async Task RunFifoOrder(IDispatchAdapter adapter, int requestCount)
    {
        _cts = new CancellationTokenSource();
        log($"=== FIFO Order: {adapter.Name} ({requestCount} requests) ===");

        var executionOrder = new ConcurrentQueue<int>();
        await Task.Run(async () =>
        {
            var tasks = new List<Task>(requestCount);
            for (var i = 0; i < requestCount; i++)
            {
                var idx = i;
                tasks.Add(adapter.RunAsync(app => { executionOrder.Enqueue(idx); }, _cts.Token));
            }
            await Task.WhenAll(tasks);
        });

        var order = executionOrder.ToArray();
        var outOfOrder = 0;
        for (var i = 1; i < order.Length; i++)
            if (order[i] < order[i - 1]) outOfOrder++;

        log($"  Total: {order.Length}, Out of order: {outOfOrder}");
        log($"  FIFO: {(outOfOrder == 0 ? "PASS" : "FAIL")}");
        log("");
    }

    public async Task RunCancellation(IDispatchAdapter adapter, int requestCount)
    {
        if (!adapter.SupportsCancellation)
        {
            log($"=== Cancellation: {adapter.Name} -- SKIPPED (no CancellationToken support) ===");
            log("");
            return;
        }

        log($"=== Cancellation: {adapter.Name} ({requestCount} req, cancel half) ===");
        var cancelCts = new CancellationTokenSource();
        var c = new Counters();

        await Task.Run(async () =>
        {
            var tasks = new List<Task>(requestCount);
            for (var i = 0; i < requestCount; i++)
            {
                var token = i % 2 == 0 ? cancelCts.Token : CancellationToken.None;
                tasks.Add(adapter.RunAsync(app => { Thread.SpinWait(100); return i; }, token));
                if (i == requestCount / 4)
                    await cancelCts.CancelAsync();
            }
            foreach (var task in tasks)
                await c.AwaitAndRecord(task);
        });

        log($"  Completed: {c.Completed}, Cancelled: {c.Cancelled}, Faulted: {c.Failed}, Hung(timeout): {c.TimedOut}");
        log("");
    }

    public async Task RunMixedWorkload(IDispatchAdapter adapter, int requestCount, int threadCount)
    {
        _cts = new CancellationTokenSource();
        log($"=== Mixed Workload: {adapter.Name} ({requestCount} req, {threadCount} threads) ===");

        var c = new Counters();
        var iteration = 0;

        await RunParallel(requestCount, threadCount, async (token, _) =>
        {
            var i = Interlocked.Increment(ref iteration);
            await c.RunGuarded(() => (i % 3) switch
            {
                0 => adapter.RunAsync(_ => { }, token),
                1 => adapter.RunAsync(app => i, token).ContinueWith(_ => { }, TaskContinuationOptions.ExecuteSynchronously),
                _ => adapter.RunAsync(app => { Thread.SpinWait(50); }, token)
            });
        });

        log($"  Completed: {c.Completed}, Failed: {c.Failed}, Hung(timeout): {c.TimedOut}");
        log($"  Deadlocks: {(c.Total == requestCount ? "NONE" : "POSSIBLE")}");
        log("");
    }

    public async Task RunErrorPropagation(IDispatchAdapter adapter, int requestCount)
    {
        _cts = new CancellationTokenSource();
        log($"=== Error Propagation: {adapter.Name} ({requestCount} requests) ===");

        var propagated = 0;
        var swallowed = 0;
        var wrongType = 0;
        var timedOut = 0;

        await Task.Run(async () =>
        {
            var tasks = new List<Task<int>>(requestCount);
            for (var i = 0; i < requestCount; i++)
            {
                var idx = i;
                tasks.Add(adapter.RunAsync<int>(app =>
                    throw new InvalidOperationException($"Test error #{idx}")));
            }
            foreach (var task in tasks)
            {
                try { await task; Interlocked.Increment(ref swallowed); }
                catch (InvalidOperationException) { Interlocked.Increment(ref propagated); }
                catch (TimeoutException) { Interlocked.Increment(ref timedOut); }
                catch { Interlocked.Increment(ref wrongType); }
            }
        });

        log($"  Propagated: {propagated}, Swallowed: {swallowed}, WrongType: {wrongType}, Hung(timeout): {timedOut}");
        log($"  Error fidelity: {(propagated == requestCount ? "PASS" : "FAIL")}");
        log("");
    }

    public async Task RunRapidReentry(IDispatchAdapter adapter, int depth)
    {
        _cts = new CancellationTokenSource();
        log($"=== Rapid Re-entry: {adapter.Name} (depth={depth}) ===");

        var sw = Stopwatch.StartNew();
        var actualDepth = 0;
        var faulted = false;
        string? error = null;

        try
        {
            await Task.Run(async () =>
            {
                for (var i = 0; i < depth; i++)
                    await adapter.RunAsync(app => { Interlocked.Increment(ref actualDepth); return i; });
            });
        }
        catch (Exception ex) { faulted = true; error = ex.GetType().Name; }

        sw.Stop();
        log($"  Depth reached: {actualDepth}/{depth}, Faulted: {faulted}{(error != null ? $" ({error})" : "")}");
        log($"  Wall time: {sw.ElapsedMilliseconds}ms");
        log("");
    }

    public async Task RunGcPressure(IDispatchAdapter adapter, int requestCount)
    {
        _cts = new CancellationTokenSource();
        log($"=== GC Pressure: {adapter.Name} ({requestCount} requests) ===");

        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var memBefore = GC.GetTotalMemory(true);
        var c = new Counters();
        var totalSw = Stopwatch.StartNew();

        await Task.Run(async () =>
        {
            for (var i = 0; i < requestCount; i++)
            {
                if (_cts.IsCancellationRequested) break;
                var payload = new byte[64];
                await c.RunGuarded(() => adapter.RunAsync(app =>
                {
                    var result = new byte[payload.Length];
                    Array.Copy(payload, result, payload.Length);
                    return result.Length;
                }));
            }
        });

        totalSw.Stop();
        var memAfter = GC.GetTotalMemory(false);

        log($"  Completed: {c.Completed}, Failed: {c.Failed}, Hung(timeout): {c.TimedOut}");
        log($"  GC gen0: +{GC.CollectionCount(0) - gen0Before}, gen1: +{GC.CollectionCount(1) - gen1Before}, gen2: +{GC.CollectionCount(2) - gen2Before}");
        log($"  Memory delta: {(memAfter - memBefore) / 1024.0:F0} KB");
        log($"  Wall time: {totalSw.ElapsedMilliseconds}ms");
        log("");
    }

    public async Task RunSustainedLoad(IDispatchAdapter adapter, int durationSeconds, int producerCount)
    {
        _cts = new CancellationTokenSource();
        log($"=== Sustained Load: {adapter.Name} ({durationSeconds}s, {producerCount} producers) ===");

        var c = new Counters();
        var deadline = TimeSpan.FromSeconds(durationSeconds);
        var totalSw = Stopwatch.StartNew();

        var tasks = new List<Task>(producerCount);
        for (var p = 0; p < producerCount; p++)
        {
            tasks.Add(Task.Run(async () =>
            {
                while (totalSw.Elapsed < deadline && !_cts.IsCancellationRequested)
                    await c.RunGuarded(() => adapter.RunAsync(_ => { }, _cts.Token));
            }));
        }

        await Task.WhenAll(tasks);
        totalSw.Stop();

        log($"  Completed: {c.Completed}, Failed: {c.Failed}, Hung(timeout): {c.TimedOut}");
        log($"  Wall time: {totalSw.ElapsedMilliseconds}ms, Sustained RPS: {c.Completed / totalSw.Elapsed.TotalSeconds:F0}");
        log("");
    }

    public async Task RunAll(IReadOnlyList<IDispatchAdapter> adapters, int requestCount, int producerCount)
    {
        foreach (var adapter in adapters)
        {
            if (_cts is { IsCancellationRequested: true }) break;

            log($"╔══════════════════════════════════════╗");
            log($"║  {adapter.Name,-34}  ║");
            log($"╚══════════════════════════════════════╝");

            await RunOverheadProfile(adapter, Math.Min(requestCount, 1000));
            await RunThroughput(adapter, requestCount, producerCount);
            await RunBurst(adapter, Math.Min(requestCount, 1000), Math.Min(producerCount * 2, 16));
            await RunFifoOrder(adapter, 200);
            await RunCancellation(adapter, 100);
            await RunErrorPropagation(adapter, 50);
            await RunMixedWorkload(adapter, Math.Min(requestCount, 500), 4);
            await RunGcPressure(adapter, Math.Min(requestCount, 500));
            await RunRapidReentry(adapter, 100);
            await RunSustainedLoad(adapter, 5, producerCount);

            log("────────────────────────────────────────");
        }
    }
}
#endif
