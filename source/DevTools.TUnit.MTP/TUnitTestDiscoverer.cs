using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.TUnit.Runtime;

namespace DevTools.TUnit.MTP;

public sealed class TUnitTestDiscoverer : ITestDiscoverer, ITestRunMapper
{
    public IReadOnlyList<TestDiscoveredTest> Discover(string assemblyPath, TestSelection selection) =>
        TUnitCatalog.Discover(assemblyPath, selection);

    public TestSelection ToRunSelection(
        TestSelection requested,
        IReadOnlyList<TestDiscoveredTest> discovered)
    {
        if (requested.Kind == TestSelectionKind.All)
            return TestSelection.All;

        if (requested.Kind == TestSelectionKind.FrameworkFilter)
            return requested;

        if (requested.Kind == TestSelectionKind.TestIds && requested.TestIds.Count == 0)
            return TestSelection.FromTestIds([]);

        var ids = discovered.Select(test => test.TestId).Distinct(StringComparer.Ordinal).ToList();
        return TestSelection.FromTestIds(ids);
    }

    public IReadOnlyList<TestCaseResult> FoldResults(
        TestSelection requested,
        IReadOnlyList<TestDiscoveredTest> discovered,
        IReadOnlyList<TestCaseResult> hostResults) =>
        hostResults;

    public IReadOnlyList<TestCaseResult> ResultsForUnreported(
        TestSelection requested,
        IReadOnlyList<TestDiscoveredTest> discovered,
        IReadOnlyList<TestCaseResult> hostResults)
    {
        _ = requested;
        var reported = hostResults.Select(result => result.TestId).ToHashSet(StringComparer.Ordinal);
        return discovered
            .Where(test => !reported.Contains(test.TestId))
            .Select(test => new TestCaseResult(
                test.TestId,
                test.DisplayName,
                TestOutcomes.Error,
                0,
                "TUnit did not report a result for the selected test.",
                null,
                null,
                test.Source,
                [],
                [],
                FullName: test.FullName))
            .ToList();
    }
}
