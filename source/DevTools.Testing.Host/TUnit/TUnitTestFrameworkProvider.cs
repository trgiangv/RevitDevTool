using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
namespace DevTools.Testing.Host.TUnit;

public sealed class TUnitTestFrameworkProvider : ITestFrameworkProvider, IDisposable
{
    private readonly FrameworkProvider _inner;

    public TUnitTestFrameworkProvider(TUnitGenerationPolicy policy, TUnitRuntimeSessionFactory factory)
    {
        _inner = new FrameworkProvider(TUnitGenerationPolicy.FrameworkId, policy, factory);
    }

    public TestFrameworkId FrameworkId => _inner.FrameworkId;

    public TestRunResponse Run(
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken) =>
        _inner.Run(request, eventSink, cancellationToken);

    public bool Cancel(Guid runId) => _inner.Cancel(runId);

    public void Dispose() => _inner.Dispose();
}
