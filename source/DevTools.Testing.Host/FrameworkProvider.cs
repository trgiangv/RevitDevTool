using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;

namespace DevTools.Testing.Host;

/// <summary>
/// Shared in-host provider: framework-id check, assembly preflight, optional
/// selection adapt, then <see cref="TestingRuntimeSessionManager"/>. First-party
/// NUnit/TUnit providers only differ in policy, factory, and adapt.
/// </summary>
internal sealed class FrameworkProvider : ITestFrameworkProvider, IDisposable
{
    private readonly TestingRuntimeSessionManager _sessions;
    private readonly Func<TestRunRequest, TestRunRequest>? _adapt;

    public FrameworkProvider(
        TestFrameworkId frameworkId,
        ITestingGenerationPolicy policy,
        ITestingRuntimeSessionFactory factory,
        Func<TestRunRequest, TestRunRequest>? adapt = null)
    {
        if (!Enum.IsDefined(frameworkId))
            throw new ArgumentOutOfRangeException(nameof(frameworkId), frameworkId, "Unknown test framework.");
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(factory);

        FrameworkId = frameworkId;
        _adapt = adapt;
        _sessions = new TestingRuntimeSessionManager(
            new TestingGenerationStore(Path.Combine(Path.GetTempPath(), "DevTools." + frameworkId)),
            policy,
            factory);
    }

    public TestFrameworkId FrameworkId { get; }

    public TestRunResponse Run(
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(eventSink);
        if (request.FrameworkId != FrameworkId)
        {
            throw new ArgumentException(
                $"{FrameworkId} provider cannot execute framework '{request.FrameworkId}'.",
                nameof(request));
        }

        var assemblyPath = TestingAssemblyPreflight.ResolveAndEnsureLoadable(request.Assembly.Path);
        var adapted = request with { Assembly = request.Assembly with { Path = assemblyPath } };
        if (_adapt is not null)
            adapted = _adapt(adapted);

        return _sessions.Run(adapted, new EventSink(eventSink), cancellationToken);
    }

    public bool Cancel(Guid runId) => _sessions.Cancel(runId);

    public void Dispose() => _sessions.Dispose();

    private sealed class EventSink(ITestEventSink sink) : ITestingRuntimeEventSink
    {
        public void Publish(TestEvent testingEvent) => sink.Publish(testingEvent);
    }
}
