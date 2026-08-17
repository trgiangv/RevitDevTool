using System.Reflection;
using DevTools.NUnit.Transport.Contracts;
using DevTools.NUnit.Transport.Runtime;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using NUnit.Framework.Api;
using NUnit.Framework.Internal;

namespace DevTools.NUnit.Runtime;

public sealed class NUnitRuntimeSession : INUnitRuntimeSession, ITestingRuntimeSession
{
    private readonly Assembly _testAssembly;
    private readonly string _assemblyPath;
    private readonly NUnitSourceLocationProvider _sourceLocationProvider;
    private readonly object _executionGate = new();
    private readonly object _runControl = new();
    private readonly NUnitTestAssemblyRunner _runner;

    private NUnitTestIdentityRegistry? _identityRegistry;
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
        _testAssembly = Guard.NotNull(testAssembly, nameof(testAssembly));
        _assemblyPath = Path.GetFullPath(Guard.NotNullOrWhiteSpace(assemblyPath, nameof(assemblyPath)));
        GenerationId = Guard.NotNullOrWhiteSpace(generationId, nameof(generationId));
        _runOnCallingThread = runOnCallingThread;
        _sourceLocationProvider = new NUnitSourceLocationProvider(_assemblyPath);
        _runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
    }

    public string GenerationId { get; }

    public NUnitDiscoverResponse Discover(NUnitDiscoverRequest request)
    {
        Guard.NotNull(request, nameof(request));

        lock (_executionGate)
        {
            ThrowIfClosedForOperation();
            ValidateAssemblyPath(request.AssemblyPath);
            var identityRegistry = EnsureLoaded();
            var filter = NUnitFilterFactory.Create(request.Filter);
            var explored = _runner.ExploreTests(filter);
            var cases = NUnitResultMapper.MapDiscovery(explored, identityRegistry, _sourceLocationProvider);
            return new NUnitDiscoverResponse(cases, GenerationId);
        }
    }

    public NUnitRunResponse Run(
        NUnitRunRequest request,
        INUnitRuntimeEventSink eventSink,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request, nameof(request));
        Guard.NotNull(eventSink, nameof(eventSink));

        lock (_executionGate)
        {
            ThrowIfClosedForOperation();
            ValidateAssemblyPath(request.AssemblyPath);
            var identityRegistry = EnsureLoaded();

            var filter = NUnitFilterFactory.Create(request.Filter);
            using var traceScope = new NUnitRunTraceScope();
            var listener = new NUnitEventListener(
                request.RunId,
                eventSink,
                identityRegistry,
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
                    ? Array.Empty<NUnitCaseResult>()
                    : NUnitResultMapper.MapRunResults(result, identityRegistry, _sourceLocationProvider);

                var cases = listener.ApplyTraceOutput(
                    NUnitRunResultMerger.Merge(frameworkCases, listener.GetAbortedCaseResults()));

                return new NUnitRunResponse(
                    request.RunId,
                    NUnitResultMapper.MapSummary(cases),
                    cases,
                    GenerationId);
            }
            finally
            {
                EndRun(request.RunId);
            }
        }
    }

    TestingRunResponse ITestingRuntimeSession.Run(
        TestingRunRequest request,
        ITestingRuntimeEventSink eventSink,
        CancellationToken cancellationToken)
    {
        var response = Run(
            new NUnitRunRequest(request.RunId, _assemblyPath, request.Selection.ProviderPayload),
            new TestingEventSink(eventSink),
            cancellationToken);
        return new TestingRunResponse(
            response.RunId,
            request.FrameworkId,
            response.GenerationId,
            response.Cases.Select(ToTestingCase).ToList(),
            response.Summary.Cancelled > 0 ? TestingCancellationState.Completed : TestingCancellationState.None,
            response.RuntimeDiagnostic?.Code,
            response.RuntimeDiagnostic?.Message);
    }

    public void Cancel(Guid runId)
    {
        if (_disposed || _disposing)
            throw new ObjectDisposedException(GetType().FullName);

        lock (_runControl)
        {
            if (_disposed || _disposing)
                throw new ObjectDisposedException(GetType().FullName);

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

    private void ThrowIfClosedForOperation()
    {
        if (_disposed || _disposing)
            throw new ObjectDisposedException(GetType().FullName);
    }

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
        var normalized = Path.GetFullPath(Guard.NotNullOrWhiteSpace(requestAssemblyPath, nameof(requestAssemblyPath)));
        if (!string.Equals(normalized, _assemblyPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Assembly path '{normalized}' does not match the loaded session assembly '{_assemblyPath}'.",
                nameof(requestAssemblyPath));
        }
    }

    private NUnitTestIdentityRegistry EnsureLoaded()
    {
        if (_loaded && _identityRegistry is not null)
            return _identityRegistry;

        var settings = NUnitRuntimeSettings.Create(Path.GetDirectoryName(_assemblyPath)!, _runOnCallingThread);
        _runner.Load(_testAssembly, settings);
        var root = _runner.ExploreTests(TestFilter.Empty);
        _identityRegistry = NUnitTestIdentityRegistry.Build(root);
        _loaded = true;
        return _identityRegistry;
    }

    private static TestingCaseResult ToTestingCase(NUnitCaseResult testCase) => new(
        testCase.Id,
        testCase.Name,
        testCase.Outcome,
        testCase.DurationMs,
        testCase.Message,
        testCase.StackTrace,
        testCase.Output,
        testCase.Source is null ? null : new TestingSourceLocation(testCase.Source.File, testCase.Source.Line),
        testCase.Traits?.Select(trait => new TestingTrait(trait.Name, trait.Value)).ToList() ?? [],
        testCase.Attachments?.Select(attachment => new TestingAttachment(
            attachment.Path,
            attachment.Name,
            attachment.ContentType,
            attachment.Base64)).ToList() ?? [],
        testCase.ParentTestId,
        testCase.FullName,
        testCase.SkipReason);

    private sealed class TestingEventSink(ITestingRuntimeEventSink sink) : INUnitRuntimeEventSink
    {
        public void Publish(NUnitRuntimeEvent runtimeEvent) => sink.Publish(new TestingRuntimeEvent(
            runtimeEvent.RunId,
            runtimeEvent.Kind,
            runtimeEvent.Case is null ? null : ToTestingCase(runtimeEvent.Case),
            runtimeEvent.Message,
            runtimeEvent.Attachment is null ? null : new TestingAttachment(
                runtimeEvent.Attachment.Path,
                runtimeEvent.Attachment.Name,
                runtimeEvent.Attachment.ContentType,
                runtimeEvent.Attachment.Base64),
            TestingCancellationState.None));
    }

    private enum RunLifecycleState
    {
        Idle,
        Accepted,
        Executing,
    }
}
