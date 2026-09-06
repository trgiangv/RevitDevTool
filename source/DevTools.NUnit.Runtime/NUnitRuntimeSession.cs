using System.Reflection;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using NUnit.Framework.Api;

namespace DevTools.NUnit.Runtime;

public sealed class NUnitRuntimeSession : ITestingRuntimeSession
{
    private readonly Assembly _testAssembly;
    private readonly string _assemblyPath;
    private readonly NUnitSourceLocationProvider _sourceLocationProvider;
    private readonly Lock _executionGate = new();
    private readonly Lock _runControl = new();
    private readonly NUnitTestAssemblyRunner _runner;

    private Guid _activeRunId;
    private Guid _pendingCancelRunId;
    private RunLifecycleState _runLifecycleState = RunLifecycleState.Idle;
    private bool _stopPending;
    private bool _loaded;
    private volatile bool _disposing;
    private volatile bool _disposed;

    private readonly bool _runOnCallingThread;

    public NUnitRuntimeSession(
        Assembly testAssembly,
        string assemblyPath,
        string generationId,
        bool runOnCallingThread = false)
    {
        ArgumentNullException.ThrowIfNull(testAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(generationId);
        _testAssembly = testAssembly;
        _assemblyPath = Path.GetFullPath(assemblyPath);
        GenerationId = generationId;
        _runOnCallingThread = runOnCallingThread;
        _sourceLocationProvider = new NUnitSourceLocationProvider(_assemblyPath);
        _runner = new NUnitTestAssemblyRunner(new NUnitTolerantAssemblyBuilder());
    }

    public string GenerationId { get; }

    public TestingRunResponse Run(
        TestingRunRequest request,
        ITestingRuntimeEventSink eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(eventSink);

        lock (_executionGate)
        {
            ThrowIfClosedForOperation();
            ValidateAssemblyPath(request.Assembly.Path);
            EnsureLoaded();

            var filter = NUnitFilterFactory.Create(request.Selection.ProviderPayload);
            using var traceScope = new TestingRunTraceScope();
            var listener = new NUnitEventListener(
                request.RunId,
                eventSink,
                _sourceLocationProvider,
                traceScope);

            BeginRun(request.RunId, cancellationToken.IsCancellationRequested);

            try
            {
                using var cancellationRegistration = cancellationToken.Register(() => Cancel(request.RunId));

                bool runStarted;
                lock (_runControl)
                {
                    runStarted = !_stopPending && _runLifecycleState == RunLifecycleState.Accepted;
                    if (runStarted)
                        _runLifecycleState = RunLifecycleState.Executing;
                }

                if (runStarted)
                {
                    _runner.RunAsync(listener, filter);

                    while (!_runner.WaitForCompletion(50))
                    {
                        lock (_runControl)
                        {
                            if (_stopPending)
                                ApplyPendingStopLocked();
                        }
                    }

                    lock (_runControl)
                    {
                        if (_stopPending)
                            ApplyPendingStopLocked();
                    }
                }

                var result = _runner.Result;
                var frameworkCases = result is null
                    ? Array.Empty<TestingCaseResult>()
                    : NUnitResultMapper.MapRunResults(result, _sourceLocationProvider);

                var cases = listener.ApplyTraceOutput(
                    NUnitRunResultMerger.Merge(frameworkCases, listener.GetAbortedCaseResults()));

                return new TestingRunResponse(
                    request.RunId,
                    request.FrameworkId,
                    GenerationId,
                    cases,
                    cases.Any(testCase => testCase.Outcome == TestingOutcomes.Cancelled)
                        ? TestingCancellationState.Completed
                        : TestingCancellationState.None,
                    null,
                    null);
            }
            finally
            {
                EndRun(request.RunId);
            }
        }
    }

    public void Cancel(Guid runId)
    {
        ObjectDisposedException.ThrowIf(_disposed || _disposing, this);

        lock (_runControl)
        {
            ObjectDisposedException.ThrowIf(_disposed || _disposing, this);

            if (_activeRunId != Guid.Empty && _activeRunId != runId)
                return;

            if (_activeRunId == Guid.Empty)
            {
                _pendingCancelRunId = runId;
                return;
            }

            _stopPending = true;
            ApplyPendingStopLocked();
        }
    }

    public void Dispose()
    {
        lock (_runControl)
        {
            if (_disposed)
                return;

            _disposing = true;
            if (_runner.IsTestRunning)
            {
                _stopPending = true;
                ApplyPendingStopLocked();
            }
        }

        lock (_executionGate)
        {
            lock (_runControl)
            {
                if (_disposed)
                {
                    _disposing = false;
                    return;
                }

                ResetRunControlStateLocked();
                _disposed = true;
                _disposing = false;
            }
        }
    }

    private void BeginRun(Guid runId, bool cancellationRequested)
    {
        lock (_runControl)
        {
            _activeRunId = runId;
            _runLifecycleState = RunLifecycleState.Accepted;
            _stopPending = cancellationRequested || _pendingCancelRunId == runId;
            if (_pendingCancelRunId == runId)
                _pendingCancelRunId = Guid.Empty;
        }
    }

    private void EndRun(Guid runId)
    {
        lock (_runControl)
        {
            if (_activeRunId == runId)
                _activeRunId = Guid.Empty;

            _runLifecycleState = RunLifecycleState.Idle;
            _stopPending = false;

            if (_pendingCancelRunId == runId)
                _pendingCancelRunId = Guid.Empty;
        }
    }

    private void ResetRunControlStateLocked()
    {
        _activeRunId = Guid.Empty;
        _pendingCancelRunId = Guid.Empty;
        _runLifecycleState = RunLifecycleState.Idle;
        _stopPending = false;
    }

    private void ThrowIfClosedForOperation() =>
        ObjectDisposedException.ThrowIf(_disposed || _disposing, this);

    private void ApplyPendingStopLocked()
    {
        if (!_stopPending || _activeRunId == Guid.Empty)
            return;

        if (_runLifecycleState == RunLifecycleState.Accepted)
            return;

        if (!_runner.IsTestRunning)
            return;

        try
        {
            _runner.StopRun(false);
            for (var attempt = 0; attempt < 20 && _runner.IsTestRunning; attempt++)
                Thread.Sleep(50);

            if (_runner.IsTestRunning)
                _runner.StopRun(true);
        }
        catch (NotSupportedException)
        {
            // NUnit MainThreadWorkItemDispatcher cannot cancel in-flight tests.
        }
    }

    private void ValidateAssemblyPath(string requestAssemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestAssemblyPath);
        var normalized = Path.GetFullPath(requestAssemblyPath);
        if (!string.Equals(normalized, _assemblyPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Assembly path '{normalized}' does not match the loaded session assembly '{_assemblyPath}'.",
                nameof(requestAssemblyPath));
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;

        var settings = NUnitRuntimeSettings.Create(Path.GetDirectoryName(_assemblyPath)!, _runOnCallingThread);
        _runner.Load(_testAssembly, settings);
        _loaded = true;
    }

    private enum RunLifecycleState
    {
        Idle,
        Accepted,
        Executing,
    }
}
