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
    private Guid _pendingCancelRunId;
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

    public TestRunResponse Run(
        TestRunRequest request,
        ITestingRuntimeEventSink eventSink,
        CancellationToken cancellationToken)
    {
        lock (_executionGate)
        {
            ValidateRun(request, cancellationToken);
            var linked = BeginRun(request.RunId, cancellationToken);

            try
            {
                return ExecuteRun(request, eventSink, linked.Token);
            }
            finally
            {
                EndRun(request.RunId, linked);
            }
        }
    }

    private void ValidateRun(TestRunRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateAssembly(request.Assembly.Path);
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

    private TestRunResponse ExecuteRun(
        TestRunRequest request,
        ITestingRuntimeEventSink eventSink,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return CreateCancelledResponse(request);

        if (request.Selection.Kind == TestSelectionKind.FrameworkFilter)
            return CreateInvalidSelectionResponse(request);

        var selection = MapToEngineSelection(request.Selection);
        var results = TUnitEngineHost.Run(_testAssembly, selection, cancellationToken);
        PublishResults(request.RunId, results, eventSink);

        var cancelled = results.Any(result => result.Outcome == TestOutcomes.Cancelled);
        return new TestRunResponse(
            request.RunId,
            request.FrameworkId,
            GenerationId,
            results,
            cancelled ? TestCancellationState.Completed : TestCancellationState.None,
            null,
            null);
    }

    private TestRunResponse CreateCancelledResponse(TestRunRequest request) =>
        new(
            request.RunId,
            request.FrameworkId,
            GenerationId,
            [],
            TestCancellationState.Completed,
            null,
            null);

    private TestRunResponse CreateInvalidSelectionResponse(TestRunRequest request) =>
        new(
            request.RunId,
            request.FrameworkId,
            GenerationId,
            [],
            TestCancellationState.None,
            "testing/invalid_request",
            "TUnit does not accept framework-filter XML. Use --test or --name.");

    private static void PublishResults(
        Guid runId,
        IReadOnlyList<TestCaseResult> results,
        ITestingRuntimeEventSink eventSink)
    {
        foreach (var result in results)
        {
            PublishOutput(runId, result, eventSink);
            eventSink.Publish(new TestEvent(
                runId,
                TestEventKinds.Case,
                result,
                null,
                null,
                TestCancellationState.None));
        }
    }

    private static void PublishOutput(
        Guid runId,
        TestCaseResult result,
        ITestingRuntimeEventSink eventSink)
    {
        if (string.IsNullOrWhiteSpace(result.Output))
            return;

        eventSink.Publish(new TestEvent(
            runId,
            TestEventKinds.Output,
            null,
            result.Output,
            null,
            TestCancellationState.None));
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

    internal static TestSelection MapToEngineSelection(
        TestSelection selection,
        string assemblyPath,
        Assembly alreadyLoaded)
    {
        if (selection.Kind != TestSelectionKind.Names)
            return selection;

        var discovered = TUnitCatalog.Discover(assemblyPath, selection, alreadyLoaded);
        return TestSelection.FromTestIds(
            discovered.Select(test => test.TestId).Distinct(StringComparer.Ordinal).ToList());
    }

    private TestSelection MapToEngineSelection(TestSelection selection) =>
        MapToEngineSelection(selection, _assemblyPath, _testAssembly);

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
