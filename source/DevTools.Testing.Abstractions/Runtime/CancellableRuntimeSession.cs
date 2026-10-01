using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Runtime;

/// <summary>
/// Shared run lifecycle for engine-driven runtime sessions (NUnit, TUnit, MSTest):
/// one run at a time, a linked cancellation token per run, a cancel that
/// arrives before its run starts, assembly-path validation and the common
/// response and event shapes. Subclasses implement only <see cref="Execute"/>.
/// </summary>
public abstract class CancellableRuntimeSession : ITestingRuntimeSession
{
    private readonly string _frameworkName;
    private readonly Lock _executionGate = new();
    private readonly Lock _runControl = new();
    private CancellationTokenSource? _runCts;
    private Guid _activeRunId;
    private Guid _pendingCancelRunId;
    private bool _disposed;

    protected CancellableRuntimeSession(string frameworkName, string assemblyPath, string generationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frameworkName);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(generationId);
        _frameworkName = frameworkName;
        AssemblyPath = Path.GetFullPath(assemblyPath);
        GenerationId = generationId;
    }

    public string GenerationId { get; }

    protected string AssemblyPath { get; }

    /// <summary>Runs under the session gate with a per-run token linked to cancel and dispose.</summary>
    public TestRunResponse Run(
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken)
    {
        lock (_executionGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateAssembly(request.Assembly.Path);

            var linked = BeginRun(request.RunId, cancellationToken);
            try
            {
                return Execute(request, eventSink, linked.Token);
            }
            finally
            {
                EndRun(request.RunId, linked);
            }
        }
    }
    /// <summary>Runs the request. The token is already linked to cancel and dispose.</summary>
    protected abstract TestRunResponse Execute(
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken);

    public void Cancel(Guid runId)
    {
        lock (_runControl)
        {
            if (_disposed)
                return;

            if (_activeRunId != Guid.Empty && _activeRunId != runId)
                return;

            if (_activeRunId == Guid.Empty)
            {
                _pendingCancelRunId = runId;
                return;
            }

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
            _pendingCancelRunId = Guid.Empty;
        }
    }

    protected TestRunResponse CreateCancelledResponse(TestRunRequest request) =>
        new(
            request.RunId,
            request.FrameworkId,
            GenerationId,
            [],
            TestCancellationState.Completed,
            null,
            null);

    protected TestRunResponse CreateEmptyResponse(TestRunRequest request) =>
        new(
            request.RunId,
            request.FrameworkId,
            GenerationId,
            [],
            TestCancellationState.None,
            null,
            null);

    protected TestRunResponse CreateInvalidSelectionResponse(TestRunRequest request, string message) =>
        new(
            request.RunId,
            request.FrameworkId,
            GenerationId,
            [],
            TestCancellationState.None,
            TestingErrorCodes.InvalidRequest,
            message);

    protected TestRunResponse CreateCompletedResponse(
        TestRunRequest request,
        IReadOnlyList<TestCaseResult> results,
        bool cancelled) =>
        new(
            request.RunId,
            request.FrameworkId,
            GenerationId,
            results,
            cancelled ? TestCancellationState.Completed : TestCancellationState.None,
            null,
            null);

    protected static void PublishResults(
        Guid runId,
        IReadOnlyList<TestCaseResult> results,
        ITestEventSink eventSink)
    {
        foreach (var result in results)
        {
            if (!string.IsNullOrWhiteSpace(result.Output))
            {
                eventSink.Publish(new TestEvent(
                    runId,
                    TestEventKinds.Output,
                    null,
                    result.Output,
                    null,
                    TestCancellationState.None));
            }

            eventSink.Publish(new TestEvent(
                runId,
                TestEventKinds.Case,
                result,
                null,
                null,
                TestCancellationState.None));
        }
    }

    private CancellationTokenSource BeginRun(Guid runId, CancellationToken cancellationToken)
    {
        lock (_runControl)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runCts = linked;
            _activeRunId = runId;

            if (_pendingCancelRunId != runId)
                return linked;

            _pendingCancelRunId = Guid.Empty;
            linked.Cancel();
            return linked;
        }
    }

    private void EndRun(Guid runId, CancellationTokenSource linked)
    {
        lock (_runControl)
        {
            linked.Dispose();
            if (_runCts == linked)
                _runCts = null;
            if (_activeRunId == runId)
                _activeRunId = Guid.Empty;
            if (_pendingCancelRunId == runId)
                _pendingCancelRunId = Guid.Empty;
        }
    }

    private void ValidateAssembly(string requestAssemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestAssemblyPath);
        var normalized = Path.GetFullPath(requestAssemblyPath);
        if (!string.Equals(normalized, AssemblyPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Assembly path '{normalized}' does not match the {_frameworkName} session assembly '{AssemblyPath}'.",
                nameof(requestAssemblyPath));
        }
    }
}
