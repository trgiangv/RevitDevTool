using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using NUnit.Framework.Interfaces;

namespace DevTools.NUnit.Runtime;

internal sealed class NUnitEventListener : ITestListener
{
    private readonly Guid _runId;
    private readonly ITestingRuntimeEventSink _eventSink;
    private readonly NUnitTestIdentityRegistry _identityRegistry;
    private readonly NUnitSourceLocationProvider? _sourceLocationProvider;
    private readonly NUnitRunTraceScope _traceScope;
    private readonly HashSet<string> _terminalCaseIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ITest> _startedCases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _traceByTestId = new(StringComparer.Ordinal);

    public NUnitEventListener(Guid runId, ITestingRuntimeEventSink eventSink,
        NUnitTestIdentityRegistry identityRegistry, NUnitSourceLocationProvider? sourceLocationProvider,
        NUnitRunTraceScope traceScope)
    {
        _runId = runId;
        _eventSink = eventSink;
        _identityRegistry = identityRegistry;
        _sourceLocationProvider = sourceLocationProvider;
        _traceScope = traceScope;
    }

    public void TestStarted(ITest test)
    {
        if (!test.IsSuite)
            _startedCases[_identityRegistry.GetTestId(test)] = test;
    }

    public void TestFinished(ITestResult result)
    {
        if (result.Test.IsSuite)
            return;
        var testId = _identityRegistry.GetTestId(result.Test);
        _startedCases.Remove(testId);
        var traceOutput = _traceScope.CompleteCase();
        if (!string.IsNullOrWhiteSpace(traceOutput))
            _traceByTestId[result.Test.FullName] = traceOutput;
        if (!_terminalCaseIds.Add(testId))
            return;

        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            _traceScope.WriteThrough(result.Output);
            Publish(TestingEventKinds.Output, null, result.Output, null);
        }
        foreach (var attachment in NUnitResultMapper.MapAttachments(result))
            Publish(TestingEventKinds.Attachment, null, null, attachment);

        var mapped = NUnitResultMapper.MapCaseResult(result, _identityRegistry, _sourceLocationProvider);
        if (_traceByTestId.TryGetValue(result.Test.FullName, out var captured))
            mapped = mapped with { Output = MergeOutput(mapped.Output, captured) };
        Publish(TestingEventKinds.Case, mapped, null, null);
    }

    public void TestOutput(TestOutput output)
    {
        if (!string.IsNullOrEmpty(output.Text))
            Publish(TestingEventKinds.Output, null, output.Text, null);
    }

    public void SendMessage(TestMessage message) { }

    public IReadOnlyList<TestingCaseResult> GetAbortedCaseResults()
    {
        var cases = new List<TestingCaseResult>(_startedCases.Count);
        foreach (var test in _startedCases.Values)
            cases.Add(new TestingCaseResult(
                _identityRegistry.GetTestId(test), test.Name, TestingOutcomes.Cancelled, 0,
                null, null, null, NUnitResultMapper.MapSource(test, _sourceLocationProvider), [], [],
                _identityRegistry.GetParentTestId(test), test.FullName));
        return cases;
    }

    internal IReadOnlyList<TestingCaseResult> ApplyTraceOutput(IReadOnlyList<TestingCaseResult> cases)
    {
        if (_traceByTestId.Count == 0)
            return cases;
        return cases.Select(testCase =>
        {
            var fullName = testCase.FullName;
            return fullName is not null && _traceByTestId.TryGetValue(fullName, out var traceOutput)
                ? testCase with { Output = MergeOutput(testCase.Output, traceOutput) }
                : testCase;
        }).ToList();
    }

    private void Publish(string kind, TestingCaseResult? testCase, string? message, TestingAttachment? attachment) =>
        _eventSink.Publish(new TestingRuntimeEvent(
            _runId, kind, testCase, message, attachment, TestingCancellationState.None));

    private static string? MergeOutput(string? nunitOutput, string? traceOutput)
    {
        var hasNunit = !string.IsNullOrWhiteSpace(nunitOutput);
        var hasTrace = !string.IsNullOrWhiteSpace(traceOutput);
        if (hasNunit && hasTrace)
            return nunitOutput!.TrimEnd() + Environment.NewLine + traceOutput!.TrimEnd();
        if (hasNunit)
            return nunitOutput;
        return hasTrace ? traceOutput : null;
    }
}
