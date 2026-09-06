using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Runtime;

namespace DevTools.NUnit.Host.Loading;

internal sealed class NUnitRuntimeSessionHandle : ITestingRuntimeSession, ITestingRuntimeSessionRetirementDiagnostics
{
    private ITestingRuntimeSession? _inner;
    private readonly AssemblyIsolationSession _isolationSession;
    private readonly string _shadowAssemblyPath;
    private AssemblyUnloadResult? _unloadResult;
    private bool _disposed;

    internal NUnitRuntimeSessionHandle(
        ITestingRuntimeSession inner,
        AssemblyIsolationSession isolationSession,
        string shadowAssemblyPath)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _isolationSession = isolationSession ?? throw new ArgumentNullException(nameof(isolationSession));
        _shadowAssemblyPath = Path.GetFullPath(shadowAssemblyPath);
        GenerationId = inner.GenerationId;
    }

    public string GenerationId { get; }

    internal Assembly GetLoadedTestAssembly()
    {
        var field = Inner.GetType().GetField("_testAssembly", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Runtime session test assembly field not found.");

        return (Assembly)field.GetValue(Inner)!;
    }

    internal static Assembly GetLoadedFrameworkAssembly()
    {
        if (!NUnitFrameworkHostShare.TryGetLoaded(out var shared))
            throw new InvalidOperationException("Host-shared nunit.framework has not been loaded for this generation.");

        return shared;
    }

    internal Assembly GetLoadedRuntimeAssembly() => Inner.GetType().Assembly;

    public TestingRunResponse Run(
        TestingRunRequest request,
        ITestingRuntimeEventSink eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestPath = Path.GetFullPath(request.Assembly.Path);
        if (!string.Equals(requestPath, _shadowAssemblyPath, StringComparison.OrdinalIgnoreCase))
        {
            request = request with
            {
                Assembly = request.Assembly with { Path = _shadowAssemblyPath },
            };
        }

        return Inner.Run(request, eventSink, cancellationToken);
    }

    public void Cancel(Guid runId) => Inner.Cancel(runId);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            _inner?.Dispose();
        }
        finally
        {
            _inner = null;
            _isolationSession.Dispose();
        }
    }

    public AssemblyUnloadResult VerifyUnload()
    {
        if (!_disposed)
            throw new ObjectDisposedException(nameof(NUnitRuntimeSessionHandle));
        return _unloadResult ??= _isolationSession.VerifyUnload();
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

    internal string FrameworkAssemblyIdentityForTesting =>
        GetLoadedFrameworkAssembly().FullName
        ?? throw new InvalidOperationException("Host-shared nunit.framework has no full name.");

    private ITestingRuntimeSession Inner => _inner
        ?? throw new ObjectDisposedException(nameof(NUnitRuntimeSessionHandle));
}
