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
            timings[idx].T0 = sw.ElapsedTicks;

            await Task.Run(async () =>
            {
                timings[idx].T0 = sw.ElapsedTicks;

                var task = adapter.RunAsync(app =>
                {
                    timings[idx].T2 = sw.ElapsedTicks;
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

        var completed = 0;
        var failed = 0;
        var timedOut = 0;
        var totalSw = Stopwatch.StartNew();

        var tasks = new List<Task>();
        var perProducer = requestCount / producerCount;

        for (var p = 0; p < producerCount; p++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (var i = 0; i < perProducer; i++)
                {
                    if (_cts.IsCancellationRequested) break;
                    try
                    {
                        await adapter.RunAsync(_ => { }, _cts.Token);
                        Interlocked.Increment(ref completed);
                    }
                    catch (TimeoutException)
                    {
                        Interlocked.Increment(ref timedOut);
                    }
                    catch
                    {
                        Interlocked.Increment(ref failed);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
        totalSw.Stop();

        var rps = completed / (totalSw.Elapsed.TotalSeconds);
        log($"  Completed: {completed}, Failed: {failed}, Hung(timeout): {timedOut}");
        log($"  Wall time: {totalSw.ElapsedMilliseconds}ms, Throughput: {rps:F0} req/s");
        log("");
    }

    public async Task RunBurst(IDispatchAdapter adapter, int requestCount, int threadCount)
    {
        _cts = new CancellationTokenSource();
        log($"=== Burst: {adapter.Name} ({requestCount} req from {threadCount} threads) ===");

        var counter = new ConcurrentBag<int>();
        var hung = 0;
        var faulted = 0;
        var barrier = new Barrier(threadCount);
        var perThread = requestCount / threadCount;
        var totalSw = Stopwatch.StartNew();

        var tasks = new List<Task>();
        for (var t = 0; t < threadCount; t++)
        {
            tasks.Add(Task.Run(async () =>
            {
                barrier.SignalAndWait();

                var localTasks = new List<Task<int>>();
                for (var i = 0; i < perThread; i++)
                {
                    localTasks.Add(adapter.RunAsync(app =>
                    {
                        counter.Add(1);
                        return 1;
                    }));
                }

                foreach (var task in localTasks)
                {
                    try
                    {
                        await task;
                    }
                    catch (TimeoutException)
                    {
                        Interlocked.Increment(ref hung);
                    }
                    catch
                    {
                        Interlocked.Increment(ref faulted);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
        totalSw.Stop();

        log($"  Expected: {requestCount}, Executed: {counter.Count}, Lost: {requestCount - counter.Count}");
        log($"  Hung(timeout): {hung}, Faulted: {faulted}");
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
            var tasks = new List<Task>();
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
        {
            if (order[i] < order[i - 1]) outOfOrder++;
        }

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
        var completed = 0;
        var cancelled = 0;
        var timedOut = 0;
        var faulted = 0;

        await Task.Run(async () =>
        {
            var tasks = new List<Task>();
            for (var i = 0; i < requestCount; i++)
            {
                var token = i % 2 == 0 ? cancelCts.Token : CancellationToken.None;
                tasks.Add(adapter.RunAsync(app =>
                {
                    Thread.SpinWait(100);
                    return i;
                }, token));

                if (i == requestCount / 4)
                {
                    await cancelCts.CancelAsync();
                }
            }

            foreach (var task in tasks)
            {
                try
                {
                    await task;
                    Interlocked.Increment(ref completed);
                }
                catch (OperationCanceledException)
                {
                    Interlocked.Increment(ref cancelled);
                }
                catch (TimeoutException)
                {
                    Interlocked.Increment(ref timedOut);
                }
                catch
                {
                    Interlocked.Increment(ref faulted);
                }
            }
        });

        log($"  Completed: {completed}, Cancelled: {cancelled}, Faulted: {faulted}, Hung(timeout): {timedOut}");
        log("");
    }

    public async Task RunMixedWorkload(IDispatchAdapter adapter, int requestCount, int threadCount)
    {
        _cts = new CancellationTokenSource();
        log($"=== Mixed Workload: {adapter.Name} ({requestCount} req, {threadCount} threads) ===");

        var completed = 0;
        var failed = 0;
        var timedOut = 0;
        var perThread = requestCount / threadCount;

        var tasks = new List<Task>();
        for (var t = 0; t < threadCount; t++)
        {
            var threadId = t;
            tasks.Add(Task.Run(async () =>
            {
                for (var i = 0; i < perThread; i++)
                {
                    if (_cts.IsCancellationRequested) break;
                    try
                    {
                        switch (i % 3)
                        {
                            case 0:
                                await adapter.RunAsync(_ => { }, _cts.Token);
                                break;
                            case 1:
                                await adapter.RunAsync(app => threadId * 1000 + i, _cts.Token);
                                break;
                            case 2:
                                await adapter.RunAsync(app =>
                                {
                                    Thread.SpinWait(50);
                                }, _cts.Token);
                                break;
                        }
                        Interlocked.Increment(ref completed);
                    }
                    catch (TimeoutException)
                    {
                        Interlocked.Increment(ref timedOut);
                    }
                    catch
                    {
                        Interlocked.Increment(ref failed);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
        log($"  Completed: {completed}, Failed: {failed}, Hung(timeout): {timedOut}");
        log($"  Deadlocks: {(completed + failed + timedOut == requestCount ? "NONE" : "POSSIBLE")}");
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
            await RunMixedWorkload(adapter, Math.Min(requestCount, 500), 4);

            log("────────────────────────────────────────");
            log("");
        }

        log("=== ALL TESTS COMPLETE ===");
    }
}
#endif
