using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Results;
using DevTools.NUnit.Core.Runtime;
using NUnit.Framework.Interfaces;

namespace DevTools.NUnit.Runtime;

internal sealed class NUnitEventListener : ITestListener
{
    private readonly Guid _runId;
    private readonly INUnitRuntimeEventSink _eventSink;
    private readonly NUnitTestIdentityRegistry _identityRegistry;
    private readonly NUnitSourceLocationProvider? _sourceLocationProvider;
    private readonly HashSet<string> _terminalCaseIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ITest> _startedCases = new(StringComparer.Ordinal);

    public NUnitEventListener(
        Guid runId,
        INUnitRuntimeEventSink eventSink,
        NUnitTestIdentityRegistry identityRegistry,
        NUnitSourceLocationProvider? sourceLocationProvider)
    {
        _runId = runId;
        _eventSink = eventSink;
        _identityRegistry = identityRegistry;
        _sourceLocationProvider = sourceLocationProvider;
    }

    public void TestStarted(ITest test)
    {
        if (test.IsSuite)
            return;

        _startedCases[_identityRegistry.GetTestId(test)] = test;
    }

    public void TestFinished(ITestResult result)
    {
        if (result.Test.IsSuite)
            return;

        var testId = _identityRegistry.GetTestId(result.Test);
        _startedCases.Remove(testId);

        if (!_terminalCaseIds.Add(testId))
            return;

        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            _eventSink.Publish(new NUnitRuntimeEvent(
                _runId,
                NUnitRuntimeEventKinds.CaseOutput,
                null,
                result.Output,
                null));
        }

        foreach (var attachment in NUnitResultMapper.MapAttachments(result) ?? [])
        {
            _eventSink.Publish(new NUnitRuntimeEvent(
                _runId,
                NUnitRuntimeEventKinds.Attachment,
                null,
                null,
                attachment));
        }

        var mapped = NUnitResultMapper.MapCaseResult(result, _identityRegistry, _sourceLocationProvider);
        _eventSink.Publish(new NUnitRuntimeEvent(_runId, NUnitRuntimeEventKinds.CaseFinished, mapped, null, null));
    }

    public void TestOutput(TestOutput output)
    {
        if (string.IsNullOrEmpty(output.Text))
            return;

        _eventSink.Publish(new NUnitRuntimeEvent(
            _runId,
            NUnitRuntimeEventKinds.CaseOutput,
            null,
            output.Text,
            null));
    }

    public void SendMessage(TestMessage message)
    {
    }

    public IReadOnlyList<NUnitCaseResult> GetAbortedCaseResults()
    {
        if (_startedCases.Count == 0)
            return [];

        var cases = new List<NUnitCaseResult>(_startedCases.Count);
        foreach (var test in _startedCases.Values)
        {
            cases.Add(new NUnitCaseResult(
                _identityRegistry.GetTestId(test),
                test.Name,
                NUnitOutcomes.Cancelled,
                0,
                null,
                null,
                null,
                _identityRegistry.GetParentTestId(test),
                null,
                NUnitResultMapper.MapDiscoveredTest(test, _identityRegistry, _sourceLocationProvider).Source,
                null,
                null));
        }

        return cases;
    }
}
