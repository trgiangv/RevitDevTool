using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions;

/// <summary>
/// The run mapper <see cref="TestingDiscovery.Register"/> uses when a provider does
/// not bring its own. It fits engines whose MTP discovery uid is the id the in-host
/// runtime filters with (TUnit, MSTest): the cases the discoverer returned for the
/// request are exactly what the host runs, so the selection is their ids and the
/// host results need no folding. NUnit supplies its own mapper because its host
/// identity differs from the testhost's.
/// </summary>
internal sealed class DefaultRunMapper : ITestRunMapper
{
    public static DefaultRunMapper Instance { get; } = new();

    private DefaultRunMapper()
    {
    }

    public TestSelection ToRunSelection(
        TestSelection requested,
        IReadOnlyList<TestDiscoveredTest> discovered)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(discovered);

        switch (requested.Kind)
        {
            case TestSelectionKind.All:
                return TestSelection.All;
            case TestSelectionKind.FrameworkFilter:
                return requested;
            case TestSelectionKind.TestIds when requested.TestIds.Count == 0:
                return TestSelection.FromTestIds([]);
            default:
                return TestSelection.FromTestIds(
                    discovered.Select(test => test.TestId).Distinct(StringComparer.Ordinal).ToList());
        }
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
        ArgumentNullException.ThrowIfNull(discovered);
        ArgumentNullException.ThrowIfNull(hostResults);

        var reported = hostResults.Select(result => result.TestId).ToHashSet(StringComparer.Ordinal);
        return discovered
            .Where(test => !reported.Contains(test.TestId))
            .Select(test => new TestCaseResult(
                test.TestId,
                test.DisplayName,
                TestOutcomes.Error,
                0,
                "The host did not report a result for the selected test.",
                null,
                null,
                test.Source,
                [],
                [],
                FullName: test.FullName))
            .ToList();
    }
}
