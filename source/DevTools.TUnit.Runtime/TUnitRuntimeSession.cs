using System.Reflection;
using System.Runtime.CompilerServices;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.TUnit.Runtime;

public sealed class TUnitRuntimeSession : ITestingRuntimeSession
{
    private readonly Assembly _testAssembly;
    private readonly string _assemblyPath;
    private readonly object _executionGate = new();
    private readonly object _runControl = new();
    private CancellationTokenSource? _runCts;
    private Guid _activeRunId;
    private bool _disposed;

    public TUnitRuntimeSession(Assembly testAssembly, string assemblyPath, string generationId)
    {
        _testAssembly = testAssembly ?? throw new ArgumentException("Value is required.", nameof(testAssembly));
        _assemblyPath = Path.GetFullPath(Required(assemblyPath, nameof(assemblyPath)));
        GenerationId = Required(generationId, nameof(generationId));
    }

    public string GenerationId { get; }

    public TestingRunResponse Run(
        TestingRunRequest request,
        ITestingRuntimeEventSink eventSink,
        CancellationToken cancellationToken)
    {
        lock (_executionGate)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            ValidateAssembly(request.Assembly.Path);

            RuntimeHelpers.RunModuleConstructor(_testAssembly.ManifestModule.ModuleHandle);

            CancellationTokenSource linked;
            lock (_runControl)
            {
                ThrowIfDisposed();
                linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _runCts = linked;
                _activeRunId = request.RunId;
            }

            try
            {
                var results = TUnitEngineHost.Run(request.Selection, linked.Token);
                foreach (var result in results)
                {
                    eventSink.Publish(new TestingRuntimeEvent(
                        request.RunId,
                        TestingEventKinds.Case,
                        result,
                        null,
                        null,
                        TestingCancellationState.None));
                }

                var cancelled = results.Any(result => result.Outcome == TestingOutcomes.Cancelled);
                return new TestingRunResponse(
                    request.RunId,
                    request.FrameworkId,
                    GenerationId,
                    results,
                    cancelled ? TestingCancellationState.Completed : TestingCancellationState.None,
                    null,
                    null);
            }
            finally
            {
                lock (_runControl)
                {
                    linked.Dispose();
                    if (_runCts == linked)
                        _runCts = null;
                    if (_activeRunId == request.RunId)
                        _activeRunId = Guid.Empty;
                }
            }
        }
    }

    public void Cancel(Guid runId)
    {
        lock (_runControl)
        {
            if (_disposed)
                return;

            if (_activeRunId == runId)
                _runCts?.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_runControl)
        {
            if (_disposed)
                return;

            _disposed = true;
            _runCts?.Cancel();
            _runCts?.Dispose();
            _runCts = null;
            _activeRunId = Guid.Empty;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().FullName);
    }

    private void ValidateAssembly(string requestAssemblyPath)
    {
        var normalized = Path.GetFullPath(requestAssemblyPath);
        if (!string.Equals(normalized, _assemblyPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Assembly path '{normalized}' does not match the TUnit session assembly '{_assemblyPath}'.",
                nameof(requestAssemblyPath));
        }
    }

    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value;
}
