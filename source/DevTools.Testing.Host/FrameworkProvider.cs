using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;

namespace DevTools.Testing.Host;

/// <summary>
/// Shared in-host provider: framework-id check, assembly preflight, then
/// <see cref="TestingRuntimeSessionManager"/>. The request selection is already
/// in the shape the runtime accepts (the testhost run mapper resolved it).
/// </summary>
internal sealed class FrameworkProvider : ITestFrameworkProvider, IDisposable
{
    private readonly TestingRuntimeSessionManager _sessions;

    public FrameworkProvider(
        ITestingGenerationPolicy policy,
        ITestingRuntimeSessionFactory factory)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(factory);
        if (!Enum.IsDefined(policy.FrameworkId))
            throw new ArgumentOutOfRangeException(nameof(policy), policy.FrameworkId, "Unknown test framework.");

        FrameworkId = policy.FrameworkId;
        _sessions = new TestingRuntimeSessionManager(
            new TestingGenerationStore(Path.Combine(Path.GetTempPath(), "DevTools." + policy.FrameworkId)),
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

        var assemblyPath = TestingGenerationFiles.RequireManagedAssembly(request.Assembly.Path);
        var resolved = request with { Assembly = new TestAssemblyReference(Path: assemblyPath) };
        return _sessions.Run(resolved, eventSink, cancellationToken);
    }

    public bool Cancel(Guid runId) => _sessions.Cancel(runId);

    public void Dispose() => _sessions.Dispose();
}
