using DevTools.NUnit.Transport.Contracts;
using DevTools.NUnit.Transport.Results;
using DevTools.NUnit.Transport.Runtime;
using DevTools.NUnit.Host.Loading;

namespace DevTools.NUnit.Host.Tests.TestSupport;

internal sealed class FakeNUnitRuntimeSessionFactory : INUnitRuntimeSessionFactory
{
    public List<NUnitGenerationManifest> CreatedManifests { get; } = [];

    public Func<NUnitGenerationManifest, INUnitRuntimeSession>? CreateImpl { get; set; }

    public INUnitRuntimeSession Create(NUnitGenerationManifest generation)
    {
        CreatedManifests.Add(generation);
        return CreateImpl?.Invoke(generation)
            ?? new FakeNUnitRuntimeSession(generation.GenerationId, generation.ShadowAssemblyPath);
    }
}

internal sealed class FakeNUnitRuntimeSession : INUnitRuntimeSession
{
    private readonly object _runLock = new();
    private Guid _activeRunId;
    private bool _cancelRequested;
    private bool _disposed;

    internal FakeNUnitRuntimeSession(string generationId, string shadowAssemblyPath)
    {
        GenerationId = generationId;
        ShadowAssemblyPath = shadowAssemblyPath;
    }

    internal string ShadowAssemblyPath { get; }

    internal int DiscoverCount { get; private set; }

    internal int RunCount { get; private set; }

    internal bool CancelRequested { get; private set; }
    internal bool IsDisposed => _disposed;

    internal Func<NUnitRunRequest, INUnitRuntimeEventSink, NUnitRunResponse>? RunImpl { get; set; }

    internal Func<NUnitDiscoverRequest, NUnitDiscoverResponse>? DiscoverImpl { get; set; }

    public string GenerationId { get; }

    public NUnitDiscoverResponse Discover(NUnitDiscoverRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        DiscoverCount++;

        if (DiscoverImpl is not null)
            return DiscoverImpl(request);

        return new NUnitDiscoverResponse(
            [
                new NUnitDiscoveredTest(
                    "fake.test#0",
                    "Fake_Test",
                    "FakeNamespace.Fake_Test"),
            ],
            GenerationId);
    }

    public NUnitRunResponse Run(
        NUnitRunRequest request,
        INUnitRuntimeEventSink eventSink,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RunCount++;

        lock (_runLock)
        {
            _activeRunId = request.RunId;
            _cancelRequested = cancellationToken.IsCancellationRequested;
        }

        try
        {
            using var registration = cancellationToken.Register(() => Cancel(request.RunId));

            if (RunImpl is not null)
                return RunImpl(request, eventSink);

            var outcome = _cancelRequested || CancelRequested
                ? NUnitOutcomes.Cancelled
                : NUnitOutcomes.Passed;

            var result = new NUnitCaseResult(
                "fake.test#0",
                "Fake_Test",
                outcome,
                1.0,
                outcome == NUnitOutcomes.Cancelled ? "Cancelled" : null,
                null,
                "fake-output");

            eventSink.Publish(new NUnitRuntimeEvent(
                request.RunId,
                "case.finished",
                result,
                null,
                null));

            return new NUnitRunResponse(
                request.RunId,
                new NUnitRunSummary(
                    outcome == NUnitOutcomes.Passed ? 1 : 0,
                    0,
                    0,
                    0,
                    0,
                    outcome == NUnitOutcomes.Cancelled ? 1 : 0),
                [result],
                GenerationId);
        }
        finally
        {
            lock (_runLock)
                _activeRunId = Guid.Empty;
        }
    }

    public void Cancel(Guid runId)
    {
        lock (_runLock)
        {
            if (_activeRunId == Guid.Empty || _activeRunId == runId)
            {
                CancelRequested = true;
                _cancelRequested = true;
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

internal sealed class RecordingRuntimeEventSink : INUnitRuntimeEventSink
{
    public List<NUnitRuntimeEvent> Events { get; } = [];

    public void Publish(NUnitRuntimeEvent runtimeEvent) => Events.Add(runtimeEvent);
}
