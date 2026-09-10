using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.TUnit.Runtime;

namespace DevTools.TUnit.MTP;

public sealed class TUnitHostTestDiscoverer : IHostTestDiscoverer, IHostTestRunMapper
{
    public IReadOnlyList<TestingDiscoveredTest> Discover(string assemblyPath, TestingSelection selection) =>
        TUnitCatalog.Discover(assemblyPath, selection);

    public TestingSelection ToHostSelection(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered)
    {
        if (requested.Kind == TestingSelectionKind.All)
            return TestingSelection.All;

        if (requested.Kind == TestingSelectionKind.FrameworkFilter)
            return requested;

        if (requested.Kind == TestingSelectionKind.TestIds && requested.TestIds.Count == 0)
            return TestingSelection.FromTestIds([]);

        var ids = discovered.Select(test => test.TestId).Distinct(StringComparer.Ordinal).ToList();
        return TestingSelection.FromTestIds(ids);
    }

    public IReadOnlyList<TestingCaseResult> FoldResults(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults) =>
        hostResults;

    public IReadOnlyList<TestingCaseResult> ResultsForUnreported(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults)
    {
        _ = requested;
        var reported = hostResults.Select(result => result.TestId).ToHashSet(StringComparer.Ordinal);
        return discovered
            .Where(test => !reported.Contains(test.TestId))
            .Select(test => new TestingCaseResult(
                test.TestId,
                test.DisplayName,
                TestingOutcomes.Error,
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
