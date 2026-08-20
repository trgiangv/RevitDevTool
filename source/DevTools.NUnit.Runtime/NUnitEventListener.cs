using System.Runtime.CompilerServices;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using NUnit.Framework.Interfaces;

namespace DevTools.NUnit.Runtime;

internal sealed class NUnitEventListener : ITestListener
{
    private readonly Guid _runId;
    private readonly ITestingRuntimeEventSink _eventSink;
    private readonly NUnitSourceLocationProvider? _sourceLocationProvider;
    private readonly NUnitRunTraceScope _traceScope;
    private readonly HashSet<ITest> _terminalCases = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<ITest> _startedCases = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, string?> _traceByFullName = new(StringComparer.Ordinal);

    public NUnitEventListener(Guid runId, ITestingRuntimeEventSink eventSink,
        NUnitSourceLocationProvider? sourceLocationProvider,
        NUnitRunTraceScope traceScope)
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
            Publish(TestingEventKinds.Output, null, result.Output, null);
        }
        foreach (var attachment in NUnitResultMapper.MapAttachments(result))
            Publish(TestingEventKinds.Attachment, null, null, attachment);

        var mapped = NUnitResultMapper.MapCaseResult(result, _sourceLocationProvider);
        if (_traceByFullName.TryGetValue(result.Test.FullName, out var captured))
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
        foreach (var test in _startedCases)
            cases.Add(new TestingCaseResult(
                NUnitTestIdentity.Id(test), test.Name, TestingOutcomes.Cancelled, 0,
                null, null, null, NUnitResultMapper.MapSource(test, _sourceLocationProvider), [], [],
                NUnitTestIdentity.ParentId(test), test.FullName));
        return cases;
    }

    internal IReadOnlyList<TestingCaseResult> ApplyTraceOutput(IReadOnlyList<TestingCaseResult> cases)
    {
        if (_traceByFullName.Count == 0)
            return cases;
        return cases.Select(testCase =>
        {
            var fullName = testCase.FullName;
            return fullName is not null && _traceByFullName.TryGetValue(fullName, out var traceOutput)
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

    private sealed class ReferenceEqualityComparer : IEqualityComparer<ITest>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        public bool Equals(ITest? x, ITest? y) => ReferenceEquals(x, y);

        public int GetHashCode(ITest obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
