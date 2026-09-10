using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions;

/// <summary>
/// Maps MTP discovery identities to in-host selection and folds host results
/// back onto IDE test-node ids. Registered atomically with the discoverer
/// through <see cref="TestingDiscovery.Register"/>.
/// </summary>
public interface ITestRunMapper
{
    TestSelection ToRunSelection(
        TestSelection requested,
        IReadOnlyList<TestDiscoveredTest> discovered);

    IReadOnlyList<TestCaseResult> FoldResults(
        TestSelection requested,
        IReadOnlyList<TestDiscoveredTest> discovered,
        IReadOnlyList<TestCaseResult> hostResults);

    IReadOnlyList<TestCaseResult> ResultsForUnreported(
        TestSelection requested,
        IReadOnlyList<TestDiscoveredTest> discovered,
        IReadOnlyList<TestCaseResult> hostResults);
}
