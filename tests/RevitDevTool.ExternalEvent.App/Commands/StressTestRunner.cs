#if DEBUG
using RevitDevTool.ExternalEvent.App.Commands.Scenarios;
namespace RevitDevTool.ExternalEvent.App.Commands;

internal sealed class StressTestRunner(Action<string> log)
{
    private CancellationTokenSource _cts = new();
    private readonly BenchmarkReport _report = new();
    private readonly Random _rng = new();

    public void Cancel() => _cts.Cancel();

    public Task RunSequentialLatency(IDispatchAdapter adapter, int requestCount) =>
        LatencyScenarios.RunSequentialLatency(adapter, requestCount, log, _report, _cts);

    public Task RunSequentialLatencyWithWorkload(IDispatchAdapter adapter, int requestCount, WorkloadProfile profile) =>
        LatencyScenarios.RunSequentialLatency(adapter, requestCount, log, _report, _cts, profile);

    public Task RunProducerSequential(IDispatchAdapter adapter, int requestCount, int producerCount) =>
        LoadScenarios.RunProducerSequential(adapter, requestCount, producerCount, log, _report, _cts);

    public Task RunTrueBurst(IDispatchAdapter adapter, int requestCount, int threadCount) =>
        LoadScenarios.RunTrueBurst(adapter, requestCount, threadCount, log, _report, _cts);

    public Task RunSustainedLoad(IDispatchAdapter adapter, int durationSeconds, int producerCount, int maxInFlight = 50) =>
        LoadScenarios.RunSustainedLoad(adapter, durationSeconds, producerCount, maxInFlight, log, _report, _cts);

    public Task RunDirectInvocation(IDispatchAdapter adapter, int requestCount) =>
        CapabilityScenarios.RunDirectInvocation(adapter, requestCount, log, _report, _cts);

    public Task RunNestedReentry(IDispatchAdapter adapter, int depth) =>
        CapabilityScenarios.RunNestedReentry(adapter, depth, log, _report, _cts);

    public Task RunCancellationLifecycle(IDispatchAdapter adapter, int requestCount) =>
        CapabilityScenarios.RunCancellationLifecycle(adapter, requestCount, log, _report, _cts);

    public Task RunErrorPropagation(IDispatchAdapter adapter, int requestCount) =>
        CapabilityScenarios.RunErrorPropagation(adapter, requestCount, log, _report, _cts);

    public Task RunFifoOrder(IDispatchAdapter adapter, int requestCount) =>
        CapabilityScenarios.RunFifoOrder(adapter, requestCount, log, _cts);

    public Task RunGcPressure(IDispatchAdapter adapter, int requestCount) =>
        CapabilityScenarios.RunGcPressure(adapter, requestCount, log, _cts);

    public Task RunFixedSequentialRaise(IFixedEventAdapter adapter, int requestCount) =>
        FixedEventScenarios.RunSequentialRaiseLatency(adapter, requestCount, log, _report, _cts);

    public Task RunFixedDirectInvocation(IFixedEventAdapter adapter, int requestCount) =>
        FixedEventScenarios.RunDirectInvocation(adapter, requestCount, log, _report, _cts);

    public Task RunFixedConcurrentRaise(IFixedEventAdapter adapter, int requestCount, int threadCount) =>
        FixedEventScenarios.RunConcurrentRaise(adapter, requestCount, threadCount, log, _report, _cts);

    public Task RunFixedGcPressure(IFixedEventAdapter adapter, int requestCount) =>
        FixedEventScenarios.RunGcPressure(adapter, requestCount, log, _cts);

    public async Task RunAll(
        IReadOnlyList<IDispatchAdapter> dispatchers,
        IReadOnlyList<IFixedEventAdapter> fixedAdapters,
        int requestCount, int producerCount)
    {
        _cts = new CancellationTokenSource();

        log("══════════════════════════════════════════════════════════════");
        log("  BENCHMARK DISCLAIMER");
        log("  This benchmark compares dispatcher behavior under selected");
        log("  execution scenarios. It is not a general ranking.");
        log("══════════════════════════════════════════════════════════════");
        log("");

        await RunDispatcherSuite(dispatchers, requestCount, producerCount);

        if (_cts.IsCancellationRequested) goto summary;

        await RunFixedEventSuite(fixedAdapters, requestCount, producerCount);

        summary:
        log("");
        log("=== SUMMARY ===");
        log(_report.ToMarkdown());
    }

    private async Task RunDispatcherSuite(
        IReadOnlyList<IDispatchAdapter> adapters, int requestCount, int producerCount)
    {
        if (adapters.Count == 0) return;

        log("══════════ SUITE 1: CENTRAL DISPATCHER ══════════");
        log("  Tests arbitrary delegate execution via RunAsync(Func/Action).");
        log("");

        var shuffled = BenchmarkHelpers.Shuffle(adapters, _rng);
        log($"  Adapter order (randomized): {string.Join(", ", shuffled.Select(a => a.Name))}");
        log("");
        foreach (var a in shuffled)
            log($"    • {a.Name} — {a.DispatchModel}");
        log("");

        foreach (var adapter in shuffled)
        {
            if (_cts.IsCancellationRequested) break;

            log($"╔══════════════════════════════════════════════════╗");
            log($"║  {adapter.Name,-46}  ║");
            log($"║  {adapter.DispatchModel,-46}  ║");
            log($"╚══════════════════════════════════════════════════╝");

            await RunSequentialLatency(adapter, Math.Min(requestCount, 1000));
            await RunSequentialLatencyWithWorkload(adapter, Math.Min(requestCount, 200), WorkloadProfile.LightRevitRead);
            await RunProducerSequential(adapter, requestCount, producerCount);
            await RunTrueBurst(adapter, Math.Min(requestCount, 1000), Math.Min(producerCount * 2, 16));
            await RunSustainedLoad(adapter, 5, producerCount);
            await RunDirectInvocation(adapter, Math.Min(requestCount, 100));
            await RunNestedReentry(adapter, 50);
            await RunCancellationLifecycle(adapter, 100);
            await RunErrorPropagation(adapter, 50);
            await RunFifoOrder(adapter, 200);
            await RunGcPressure(adapter, Math.Min(requestCount, 500));

            log("────────────────────────────────────────────────────");
        }
    }

    public async Task RunFixedEventSuite(
        IReadOnlyList<IFixedEventAdapter> adapters, int requestCount, int producerCount)
    {
        if (adapters.Count == 0) return;

        log("");
        log("══════════ SUITE 2: FIXED EVENT REUSE ══════════");
        log("  Tests fixed-handler event dispatch overhead (no per-call delegate).");
        log("");

        var shuffled = BenchmarkHelpers.Shuffle(adapters, _rng);
        log($"  Adapter order (randomized): {string.Join(", ", shuffled.Select(a => a.Name))}");
        log("");
        foreach (var a in shuffled)
            log($"    • {a.Name} — {a.DispatchModel}");
        log("");

        foreach (var adapter in shuffled)
        {
            if (_cts.IsCancellationRequested) break;

            log($"╔══════════════════════════════════════════════════╗");
            log($"║  {adapter.Name,-46}  ║");
            log($"║  {adapter.DispatchModel,-46}  ║");
            log($"╚══════════════════════════════════════════════════╝");

            await RunFixedSequentialRaise(adapter, Math.Min(requestCount, 1000));
            await RunFixedDirectInvocation(adapter, Math.Min(requestCount, 100));
            await RunFixedConcurrentRaise(adapter, Math.Min(requestCount, 1000), Math.Min(producerCount * 2, 16));
            await RunFixedGcPressure(adapter, Math.Min(requestCount, 500));

            log("────────────────────────────────────────────────────");
        }
    }
}
#endif
