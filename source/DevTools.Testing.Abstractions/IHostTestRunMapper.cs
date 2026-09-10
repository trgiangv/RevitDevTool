using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions;

/// <summary>
/// Maps MTP discovery identities to in-host selection and folds host results
/// back onto IDE test-node ids. Registered atomically with the discoverer
/// through <see cref="HostTestDiscovery.Register"/>.
/// </summary>
public interface IHostTestRunMapper
{
    TestingSelection ToHostSelection(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered);

    IReadOnlyList<TestingCaseResult> FoldResults(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults);

    IReadOnlyList<TestingCaseResult> ResultsForUnreported(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults);
}
