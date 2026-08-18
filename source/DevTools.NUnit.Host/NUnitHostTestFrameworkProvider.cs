using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host;
using DevTools.Testing.Host.Runtime;

namespace DevTools.NUnit.Host;

public static class NUnitFramework
{
    public const string Id = "nunit";
}

/// <summary>NUnit provider over the framework-neutral testing runtime session manager.</summary>
public sealed class NUnitHostTestFrameworkProvider(TestingRuntimeSessionManager sessions) :
    IHostTestFrameworkProvider,
    IDisposable
{
    public string FrameworkId => NUnitFramework.Id;

    public TestingRunResponse Run(TestingRunRequest request, ITestingEventSink eventSink,
        CancellationToken cancellationToken)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (eventSink is null)
            throw new ArgumentNullException(nameof(eventSink));
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
        return sessions.Run(normalized, new EventSink(eventSink), cancellationToken);
    }

    public bool Cancel(Guid runId)
    {
        sessions.Cancel(runId);
        return true;
    }

    public void Dispose() => sessions.Dispose();

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
