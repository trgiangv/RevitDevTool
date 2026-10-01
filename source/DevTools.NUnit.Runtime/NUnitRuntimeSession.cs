using System.Reflection;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using NUnit.Framework.Api;

namespace DevTools.NUnit.Runtime;

public sealed class NUnitRuntimeSession : CancellableRuntimeSession
{
    private const int StopPollMilliseconds = 50;

    private readonly Assembly _testAssembly;
    private readonly NUnitLocationProvider _locationProvider;
    private readonly NUnitTestAssemblyRunner _runner;
    private readonly bool _runOnCallingThread;
    private bool _loaded;

    public NUnitRuntimeSession(
        Assembly testAssembly,
        string assemblyPath,
        string generationId,
        bool runOnCallingThread = false)
        : base("NUnit", assemblyPath, generationId)
    {
        ArgumentNullException.ThrowIfNull(testAssembly);
        _testAssembly = testAssembly;
        _runOnCallingThread = runOnCallingThread;
        _locationProvider = new NUnitLocationProvider(AssemblyPath);
        _runner = new NUnitTestAssemblyRunner(new NUnitAssemblyBuilder());
    }

    protected override TestRunResponse Execute(
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken)
    {
        EnsureLoaded();

        var filter = request.Selection.Kind switch
        {
            TestSelectionKind.All => NUnitFilterFactory.Create(null),
            TestSelectionKind.FrameworkFilter when string.Equals(
                    request.Selection.FilterFormat, NUnitFilterFactory.XmlFilterFormat, StringComparison.Ordinal) =>
                NUnitFilterFactory.Create(request.Selection.FilterData),
            _ => throw new ArgumentException(
                "NUnit runtime expects All or a '" + NUnitFilterFactory.XmlFilterFormat
                + "' FrameworkFilter. The testhost run mapper resolves TestIds and Names.",
                nameof(request)),
        };
        using var traceScope = new TestRunTraceScope();
        var listener = new NUnitEventListener(
            request.RunId,
            eventSink,
            _locationProvider,
            traceScope);

        if (cancellationToken.IsCancellationRequested)
            return CreateCancelledResponse(request);

        // Stop at once when the token fires: with the main-thread dispatcher RunAsync blocks
        // until the run ends, so the request has to be armed before it. StopRun is a no-op
        // while no run is live, hence the poll below for the worker dispatcher.
        using var stopOnCancel = cancellationToken.Register(TryStopRun);
        _runner.RunAsync(listener, filter);

        while (!_runner.WaitForCompletion(StopPollMilliseconds))
        {
            if (cancellationToken.IsCancellationRequested)
                TryStopRun();
        }

        var result = _runner.Result;
        var frameworkCases = result is null
            ? Array.Empty<TestCaseResult>()
            : NUnitResultMapper.MapRunResults(result, _locationProvider);
        var cases = listener.ApplyTraceOutput(
            NUnitRunResultMerger.Merge(frameworkCases, listener.GetAbortedCaseResults()));

        return CreateCompletedResponse(
            request,
            cases,
            cases.Any(testCase => testCase.Outcome == TestOutcomes.Cancelled));
    }

    private void TryStopRun()
    {
        try
        {
            _runner.StopRun();
        }
        catch (NotSupportedException)
        {
            // NUnit's MainThreadWorkItemDispatcher cannot cancel in-flight tests.
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;

        var settings = NUnitRuntimeSettings.Create(Path.GetDirectoryName(AssemblyPath)!, _runOnCallingThread);
        _runner.Load(_testAssembly, settings);
        _loaded = true;
    }
}
