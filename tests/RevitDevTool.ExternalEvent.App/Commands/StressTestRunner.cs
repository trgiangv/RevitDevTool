#if DEBUG
using RevitDevTool.ExternalEvent.App.Commands.Scenarios;
namespace RevitDevTool.ExternalEvent.App.Commands;

internal sealed class StressTestRunner(Action<string> log)
{
    private CancellationTokenSource _cts = new();
    private readonly BenchmarkReport _report = new();
    private readonly Random _rng = new();

    public void Cancel() => _cts.Cancel();

    public Task RunSequentialLatency(IDispatchAdapter adapter, int requestCount, WorkloadProfile profile = WorkloadProfile.NoOp) =>
        LatencyScenarios.RunSequentialLatency(adapter, requestCount, log, _report, _cts, profile);

    public Task RunProducerSequential(IDispatchAdapter adapter, int requestCount, int producerCount) =>
        LoadScenarios.RunProducerSequential(adapter, requestCount, producerCount, log, _report, _cts);

    public Task RunTrueBurst(IDispatchAdapter adapter, int requestCount, int threadCount) =>
        LoadScenarios.RunTrueBurst(adapter, requestCount, threadCount, log, _report, _cts);

    public Task RunSustainedLoad(IDispatchAdapter adapter, int durationSeconds, int producerCount, int maxInFlight = 50) =>
        LoadScenarios.RunSustainedLoad(adapter, durationSeconds, producerCount, maxInFlight, log, _report, _cts);

    public Task RunDirectInvocation(IDispatchAdapter adapter, int requestCount) =>
        DispatcherCapabilityScenarios.RunDirectInvocation(adapter, requestCount, log, _report, _cts);

    public Task RunNestedReentry(IDispatchAdapter adapter, int depth) =>
        DispatcherCapabilityScenarios.RunNestedReentry(adapter, depth, log, _report, _cts);

    public Task RunCancellationLifecycle(IDispatchAdapter adapter, int requestCount) =>
        DispatcherCapabilityScenarios.RunCancellationLifecycle(adapter, requestCount, log, _report, _cts);

    public Task RunErrorPropagation(IDispatchAdapter adapter, int requestCount) =>
        DispatcherCapabilityScenarios.RunErrorPropagation(adapter, requestCount, log, _report, _cts);

    public Task RunFifoOrder(IDispatchAdapter adapter, int requestCount) =>
        DispatcherCapabilityScenarios.RunFifoOrder(adapter, requestCount, log, _cts);

    public Task RunInContextSequentialRaise(IInContextEventAdapter adapter, int requestCount) =>
        InContextEventScenarios.RunSequentialRaiseLatency(adapter, requestCount, log, _report, _cts);

    public Task RunInContextDirectInvocation(IInContextEventAdapter adapter, int requestCount) =>
        InContextEventScenarios.RunDirectInvocation(adapter, requestCount, log, _report, _cts);

    public Task RunInContextConcurrentRaise(IInContextEventAdapter adapter, int requestCount, int threadCount) =>
        InContextEventScenarios.RunConcurrentRaise(adapter, requestCount, threadCount, log, _report, _cts);

    public async Task RunAll(
        IReadOnlyList<IDispatchAdapter> dispatchers,
        IReadOnlyList<IInContextEventAdapter> inContextAdapters,
        int requestCount, int producerCount)
    {
        _cts = new CancellationTokenSource();

        log("> **Disclaimer:** This benchmark compares dispatcher behavior under selected execution scenarios. It is not a general ranking.");

        await RunDispatcherSuite(dispatchers, requestCount, producerCount);

        if (_cts.IsCancellationRequested) goto summary;

        await RunInContextEventSuite(inContextAdapters, requestCount, producerCount);

        summary:
        log("---");
        log(_report.ToMarkdown());
    }

    private async Task RunDispatcherSuite(
        IReadOnlyList<IDispatchAdapter> adapters, int requestCount, int producerCount)
    {
        if (adapters.Count == 0) return;

        log("# Suite 1: Central Dispatcher");
        log("*Tests arbitrary delegate execution via RunAsync(Func/Action).*");

        var shuffled = BenchmarkHelpers.Shuffle(adapters, _rng);
        log($"**Adapter order (randomized):** {string.Join(", ", shuffled.Select(a => a.Name))}");
        foreach (var a in shuffled)
            log($"- **{a.Name}** — {a.DispatchModel}");

        foreach (var adapter in shuffled)
        {
            if (_cts.IsCancellationRequested) break;

            log($"## {adapter.Name}");
            log($"*{adapter.DispatchModel}*");

            await RunSequentialLatency(adapter, Math.Min(requestCount, 1000));
            await RunSequentialLatency(adapter, Math.Min(requestCount, 200), WorkloadProfile.LightRevitRead);
            await RunSequentialLatency(adapter, Math.Min(requestCount, 50), WorkloadProfile.TransactionRollback);
            await RunProducerSequential(adapter, requestCount, producerCount);
            await RunTrueBurst(adapter, Math.Min(requestCount, 1000), Math.Min(producerCount * 2, 16));
            await RunSustainedLoad(adapter, 5, producerCount);
            await RunDirectInvocation(adapter, Math.Min(requestCount, 100));
            await RunNestedReentry(adapter, 50);
            await RunCancellationLifecycle(adapter, 100);
            await RunErrorPropagation(adapter, 50);
            await RunFifoOrder(adapter, 200);

            log("---");
        }
    }

    private async Task RunInContextEventSuite(
        IReadOnlyList<IInContextEventAdapter> adapters, int requestCount, int producerCount)
    {
        if (adapters.Count == 0) return;
        log("# Suite 2: In-Context Event Reuse");
        log("*Tests in-context event dispatch overhead (no per-call delegate).*");

        var shuffled = BenchmarkHelpers.Shuffle(adapters, _rng);
        log($"**Adapter order (randomized):** {string.Join(", ", shuffled.Select(a => a.Name))}");
        foreach (var a in shuffled)
            log($"- **{a.Name}** — {a.DispatchModel}");

        foreach (var adapter in shuffled)
        {
            if (_cts.IsCancellationRequested) break;

            log($"## {adapter.Name}");
            log($"*{adapter.DispatchModel}*");

            await RunInContextSequentialRaise(adapter, Math.Min(requestCount, 1000));
            await RunInContextDirectInvocation(adapter, Math.Min(requestCount, 100));
            await RunInContextConcurrentRaise(adapter, Math.Min(requestCount, 1000), Math.Min(producerCount * 2, 16));

            log("---");
        }
    }
}
#endif
