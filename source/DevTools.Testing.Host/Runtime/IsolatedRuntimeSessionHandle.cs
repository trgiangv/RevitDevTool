using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.Testing.Host.Runtime;

internal sealed class IsolatedRuntimeSessionHandle : ITestingRuntimeSession, ITestingRuntimeSessionRetirementDiagnostics
{
    private ITestingRuntimeSession? _inner;
    private readonly AssemblyIsolationSession _isolation;
    private readonly string _shadowAssemblyPath;
    private AssemblyUnloadResult? _unloadResult;

    internal IsolatedRuntimeSessionHandle(
        ITestingRuntimeSession inner,
        AssemblyIsolationSession isolation,
        string shadowAssemblyPath)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _isolation = isolation ?? throw new ArgumentNullException(nameof(isolation));
        _shadowAssemblyPath = Path.GetFullPath(shadowAssemblyPath);
        GenerationId = inner.GenerationId;
    }

    public string GenerationId { get; }

    internal ITestingRuntimeSession RuntimeSession => Inner;

    public TestRunResponse Run(
        TestRunRequest request,
        ITestingRuntimeEventSink eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestPath = Path.GetFullPath(request.Assembly.Path);
        if (!string.Equals(requestPath, _shadowAssemblyPath, StringComparison.OrdinalIgnoreCase))
        {
            request = request with
            {
                Assembly = new TestAssemblyReference(_shadowAssemblyPath),
            };
        }

        return Inner.Run(request, eventSink, cancellationToken);
    }

    public void Cancel(Guid runId) => Inner.Cancel(runId);

    public void Dispose()
    {
        if (_inner is null)
            return;

        try
        {
            _inner.Dispose();
        }
        finally
        {
            _inner = null;
            _isolation.Dispose();
        }
    }

    public AssemblyUnloadResult VerifyUnload()
    {
        if (_inner is not null)
            throw new ObjectDisposedException(nameof(IsolatedRuntimeSessionHandle));
        return _unloadResult ??= _isolation.VerifyUnload();
    }

    public TestingGenerationRetirementDiagnostic? GetRetirementDiagnostic()
    {
        var result = VerifyUnload();
        return result.IsUnloaded
            ? null
            : new TestingGenerationRetirementDiagnostic(
                GenerationId,
                "generation.retained",
                result.Detail ?? "Generation ALC retained after unload verification.");
    }

    private ITestingRuntimeSession Inner => _inner
        ?? throw new ObjectDisposedException(nameof(IsolatedRuntimeSessionHandle));
}
