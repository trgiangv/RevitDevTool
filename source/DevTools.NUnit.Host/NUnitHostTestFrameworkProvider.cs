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
        var assembly = request.Assembly with { Path = assemblyPath };
        var selection = request.Selection.Kind == TestingSelectionKind.All
            ? TestingSelection.All
            : request.Selection.Kind == TestingSelectionKind.FrameworkFilter
                ? request.Selection
                : TestingSelection.FromFrameworkFilter(
                    TestingSelection.XmlFilterFormat,
                    NUnitSelectionFilter.ToNUnitFilter(request.Selection)
                    ?? throw new InvalidOperationException("NUnit selection mapping produced no filter."));
        return _sessions.Run(
            request with { Assembly = assembly, Selection = selection },
            new EventSink(eventSink),
            cancellationToken);
    }

    public bool Cancel(Guid runId) => _sessions.Cancel(runId);

    public void Dispose() => _sessions.Dispose();

    private sealed class EventSink(ITestingEventSink sink) : ITestingRuntimeEventSink
    {
        public void Publish(TestingEvent testingEvent) => sink.Publish(testingEvent);
    }
}
