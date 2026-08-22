using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.TUnit.Runtime;

namespace DevTools.TUnit.MTP;

internal sealed class TUnitHostTestDiscoverer : IHostTestDiscoverer, IHostTestRunMapper
{
    public IReadOnlyList<TestingDiscoveredTest> Discover(string assemblyPath) =>
        Discover(assemblyPath, TestingDiscoveryOptions.Testhost);

    public IReadOnlyList<TestingDiscoveredTest> Discover(string assemblyPath, TestingDiscoveryOptions options) =>
        Select(assemblyPath, new TestingSelection([]), options);

    public IReadOnlyList<TestingDiscoveredTest> Select(string assemblyPath, TestingSelection selection) =>
        Select(assemblyPath, selection, TestingDiscoveryOptions.Testhost);

    public IReadOnlyList<TestingDiscoveredTest> Select(
        string assemblyPath,
        TestingSelection selection,
        TestingDiscoveryOptions options) =>
        TUnitCatalog.Discover(assemblyPath, selection, options);

    public TestingSelection ToHostSelection(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered)
    {
        if (requested.TestIds.Count == 0 && (requested.Names?.Count ?? 0) == 0)
            return new TestingSelection([], Hints: requested.Hints);

        return new TestingSelection(
            discovered.Select(test => test.TestId).Distinct(StringComparer.Ordinal).ToList(),
            Hints: requested.Hints is { IsEmpty: false }
                ? requested.Hints
                : TUnitTestIdentity.ToHints(discovered));
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
