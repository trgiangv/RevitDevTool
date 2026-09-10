using System.Runtime.CompilerServices;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using NUnit.Framework.Interfaces;
using TestAttachment = DevTools.Testing.Abstractions.Contracts.TestAttachment;
using TestCaseResult = DevTools.Testing.Abstractions.Contracts.TestCaseResult;

namespace DevTools.NUnit.Runtime;

internal sealed class NUnitEventListener : ITestListener
{
    private readonly Guid _runId;
    private readonly ITestingRuntimeEventSink _eventSink;
    private readonly NUnitSourceLocationProvider? _sourceLocationProvider;
    private readonly TestRunTraceScope _traceScope;
    private readonly HashSet<ITest> _terminalCases = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<ITest> _startedCases = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, string?> _traceByFullName = new(StringComparer.Ordinal);

    public NUnitEventListener(Guid runId, ITestingRuntimeEventSink eventSink,
        NUnitSourceLocationProvider? sourceLocationProvider,
        TestRunTraceScope traceScope)
    {
        _runId = runId;
        _eventSink = eventSink;
        _sourceLocationProvider = sourceLocationProvider;
        _traceScope = traceScope;
    }

    public void TestStarted(ITest test)
    {
        if (!test.IsSuite)
            _startedCases.Add(test);
    }

    public void TestFinished(ITestResult result)
    {
        if (result.Test.IsSuite)
            return;
        _startedCases.Remove(result.Test);
        var traceOutput = _traceScope.CompleteCase();
        if (!string.IsNullOrWhiteSpace(traceOutput))
            _traceByFullName[result.Test.FullName] = traceOutput;
        if (!_terminalCases.Add(result.Test))
            return;

        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            _traceScope.WriteThrough(result.Output);
            Publish(TestEventKinds.Output, null, result.Output, null);
        }
        foreach (var attachment in NUnitResultMapper.MapAttachments(result))
            Publish(TestEventKinds.Attachment, null, null, attachment);

        var mapped = NUnitResultMapper.MapCaseResult(result, _sourceLocationProvider);
        if (_traceByFullName.TryGetValue(result.Test.FullName, out var captured))
            mapped = mapped with { Output = TestRunTraceScope.Merge(mapped.Output, captured) };
        Publish(TestEventKinds.Case, mapped, null, null);
    }

    public void TestOutput(TestOutput output)
    {
        if (!string.IsNullOrEmpty(output.Text))
            Publish(TestEventKinds.Output, null, output.Text, null);
    }

    public void SendMessage(TestMessage message) { }

    public IReadOnlyList<TestCaseResult> GetAbortedCaseResults()
    {
        var cases = new List<TestCaseResult>(_startedCases.Count);
        foreach (var test in _startedCases)
            cases.Add(new TestCaseResult(
                NUnitTestIdentity.Id(test), test.Name, TestOutcomes.Cancelled, 0,
                null, null, null, NUnitResultMapper.MapSource(test, _sourceLocationProvider), [], [],
                NUnitTestIdentity.ParentId(test), test.FullName));
        return cases;
    }

    internal IReadOnlyList<TestCaseResult> ApplyTraceOutput(IReadOnlyList<TestCaseResult> cases)
    {
        if (_traceByFullName.Count == 0)
            return cases;
        return cases.Select(testCase =>
        {
            var fullName = testCase.FullName;
            return fullName is not null && _traceByFullName.TryGetValue(fullName, out var traceOutput)
                ? testCase with { Output = TestRunTraceScope.Merge(testCase.Output, traceOutput) }
                : testCase;
        }).ToList();
    }

    private void Publish(string kind, TestCaseResult? testCase, string? message, TestAttachment? attachment) =>
        _eventSink.Publish(new TestEvent(
            _runId, kind, testCase, message, attachment, TestCancellationState.None));

    private sealed class ReferenceEqualityComparer : IEqualityComparer<ITest>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        public bool Equals(ITest? x, ITest? y) => ReferenceEquals(x, y);

        public int GetHashCode(ITest obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
