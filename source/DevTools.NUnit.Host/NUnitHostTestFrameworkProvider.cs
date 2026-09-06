using DevTools.NUnit.Host.Loading;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;

namespace DevTools.NUnit.Host;

public static class NUnitFramework
{
    public const string Id = "nunit";
}

/// <summary>NUnit provider over the framework-neutral testing runtime session manager.</summary>
public sealed class NUnitHostTestFrameworkProvider(NUnitGenerationPolicy policy, NUnitRuntimeSessionFactory factory) : IHostTestFrameworkProvider, IDisposable
{
    private readonly TestingRuntimeSessionManager _sessions = new(
        new TestingGenerationStore(Path.Combine(Path.GetTempPath(), "DevTools", "NUnit", "Generations")),
        policy,
        factory);

    public string FrameworkId => NUnitFramework.Id;

    public TestingRunResponse Run(TestingRunRequest request, ITestingEventSink eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(eventSink);
        if (!string.Equals(request.FrameworkId, FrameworkId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"NUnit provider cannot execute framework '{request.FrameworkId}'.", nameof(request));

        var assemblyPath = TestingAssemblyPreflight.ResolveAndEnsureLoadable(request.Assembly.Path);
        var normalized = request with
        {
            Assembly = request.Assembly with { Path = assemblyPath },
            Selection = new TestingSelection(
                [],
                NUnitSelectionFilter.ToNUnitFilter(request.Selection))
        };
        return _sessions.Run(normalized, new EventSink(eventSink), cancellationToken);
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
