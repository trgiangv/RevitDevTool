using System.Reflection;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.TUnit.Runtime;

public sealed class TUnitRuntimeSession : CancellableRuntimeSession
{
    private readonly Assembly _testAssembly;

    public TUnitRuntimeSession(Assembly testAssembly, string assemblyPath, string generationId)
        : base("TUnit", assemblyPath, generationId)
    {
        ArgumentNullException.ThrowIfNull(testAssembly);
        _testAssembly = testAssembly;
    }

    protected override TestRunResponse Execute(
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return CreateCancelledResponse(request);

        if (request.Selection.Kind is TestSelectionKind.FrameworkFilter or TestSelectionKind.Names)
            return CreateInvalidSelectionResponse(request, "TUnit runs All or TestIds. The testhost run mapper resolves Names.");

        var results = TUnitEngineHost.Run(_testAssembly, request.Selection, cancellationToken);
        PublishResults(request.RunId, results, eventSink);

        return CreateCompletedResponse(
            request,
            results,
            results.Any(result => result.Outcome == TestOutcomes.Cancelled));
    }
}
