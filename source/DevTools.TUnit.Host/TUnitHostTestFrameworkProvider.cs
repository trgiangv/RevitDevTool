using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;

namespace DevTools.TUnit.Host;

public sealed class TUnitHostTestFrameworkProvider : IHostTestFrameworkProvider, IDisposable
{
    private readonly TestingRuntimeSessionManager _sessions;

    public TUnitHostTestFrameworkProvider(
        TUnitGenerationPolicy policy,
        TUnitRuntimeSessionFactory factory)
    {
        _sessions = new TestingRuntimeSessionManager(
            new TestingGenerationStore(Path.Combine(Path.GetTempPath(), "DevTools", "TUnit", "Generations")),
            policy,
            factory);
    }

    public string FrameworkId => TUnitGenerationPolicy.FrameworkId;

    public TestingRunResponse Run(
        TestingRunRequest request,
        ITestingEventSink eventSink,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.FrameworkId, FrameworkId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"TUnit provider cannot execute framework '{request.FrameworkId}'.", nameof(request));

        var assemblyPath = TestingAssemblyPreflight.ResolveAndEnsureLoadable(request.Assembly.Path);
        return _sessions.Run(
            request with { Assembly = request.Assembly with { Path = assemblyPath } },
            new EventSink(eventSink),
            cancellationToken);
    }

    public bool Cancel(Guid runId)
    {
        _sessions.Cancel(runId);
        return true;
    }

    public void Dispose() => _sessions.Dispose();

    private sealed class EventSink(ITestingEventSink sink) : ITestingRuntimeEventSink
    {
        public void Publish(TestingRuntimeEvent testingEvent) => sink.Publish(new TestingEvent(
            testingEvent.RunId,
            testingEvent.Kind,
            testingEvent.Case,
            testingEvent.Message,
            testingEvent.Attachment,
            testingEvent.CancellationState));
    }
}
