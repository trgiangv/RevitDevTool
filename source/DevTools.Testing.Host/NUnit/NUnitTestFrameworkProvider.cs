using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host.NUnit.Loading;
namespace DevTools.Testing.Host.NUnit;

/// <summary>NUnit provider over the shared in-host runtime session manager.</summary>
public sealed class NUnitTestFrameworkProvider(
    NUnitGenerationPolicy policy, 
    NUnitRuntimeSessionFactory factory) : ITestFrameworkProvider, IDisposable
{
    private readonly FrameworkProvider _inner = new(NUnitGenerationPolicy.FrameworkId, policy, factory, Map);

    public TestFrameworkId FrameworkId => _inner.FrameworkId;

    public TestRunResponse Run(
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken) =>
        _inner.Run(request, eventSink, cancellationToken);

    public bool Cancel(Guid runId) => _inner.Cancel(runId);

    public void Dispose() => _inner.Dispose();

    private static TestRunRequest Map(TestRunRequest request)
    {
        if (request.Selection.Kind is TestSelectionKind.All)
            return request;

        return request with
        {
            Selection = TestSelection.FromFrameworkFilter(
                NUnitSelectionFilter.XmlFilterFormat,
                NUnitSelectionFilter.ToNUnitFilter(request.Selection)
                ?? throw new InvalidOperationException("NUnit selection mapping produced no filter.")),
        };
    }
}
