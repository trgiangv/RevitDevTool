using System.Reflection;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.TUnit.Runtime;

public sealed class TUnitRuntimeSession : ITestingRuntimeSession
{
    private readonly Assembly _testAssembly;
    private readonly string _assemblyPath;
    private readonly Lock _executionGate = new();
    private readonly Lock _runControl = new();
    private CancellationTokenSource? _runCts;
    private Guid _activeRunId;
    private bool _disposed;

    public TUnitRuntimeSession(Assembly testAssembly, string assemblyPath, string generationId)
    {
        ArgumentNullException.ThrowIfNull(testAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(generationId);
        _testAssembly = testAssembly;
        _assemblyPath = Path.GetFullPath(assemblyPath);
        GenerationId = generationId;
    }

    public string GenerationId { get; }

    public TestingRunResponse Run(
        TestingRunRequest request,
        ITestingRuntimeEventSink eventSink,
        CancellationToken cancellationToken)
    {
        lock (_executionGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateAssembly(request.Assembly.Path);

            CancellationTokenSource linked;
            lock (_runControl)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _runCts = linked;
                _activeRunId = request.RunId;
            }

            try
            {
                var results = TUnitEngineHost.Run(_testAssembly, request.Selection, linked.Token);
                foreach (var result in results)
                {
                    if (!string.IsNullOrWhiteSpace(result.Output))
                    {
                        eventSink.Publish(new TestingRuntimeEvent(
                            request.RunId,
                            TestingEventKinds.Output,
                            null,
                            result.Output,
                            null,
                            TestingCancellationState.None));
                    }

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

    private void ValidateAssembly(string requestAssemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestAssemblyPath);
        var normalized = Path.GetFullPath(requestAssemblyPath);
        if (!string.Equals(normalized, _assemblyPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Assembly path '{normalized}' does not match the TUnit session assembly '{_assemblyPath}'.",
                nameof(requestAssemblyPath));
        }
    }
}
