using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
namespace DevTools.Testing.Host.TUnit;

public sealed class TUnitTestFrameworkProvider(
    TUnitGenerationPolicy policy, 
    TUnitRuntimeSessionFactory factory) : ITestFrameworkProvider, IDisposable
{
    private readonly FrameworkProvider _inner = new(TUnitGenerationPolicy.FrameworkId, policy, factory);

    public TestFrameworkId FrameworkId => _inner.FrameworkId;

    public TestRunResponse Run(
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken) =>
        _inner.Run(request, eventSink, cancellationToken);

    public bool Cancel(Guid runId) => _inner.Cancel(runId);

    public void Dispose() => _inner.Dispose();
}
