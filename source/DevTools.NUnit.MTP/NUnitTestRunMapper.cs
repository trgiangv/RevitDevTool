using DevTools.NUnit.Runtime;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP;

/// <summary>
/// Maps published testhost identities onto in-host filter XML and folds
/// host results onto discovered leaf uids. Selection uses
/// <see cref="NUnitIdentityIndex"/> — grouping uids are not published.
/// </summary>
public sealed class NUnitTestRunMapper : ITestRunMapper
{
    internal const string UnreportedFullNameMessage =
        "Host NUnit did not report this FullName. UID is ITest.FullName from testhost ExploreTests; in-host source expansion uses a different FullName.";

    public TestSelection ToRunSelection(
        TestSelection requested,
        IReadOnlyList<TestDiscoveredTest> discovered)
    {
        switch (requested.Kind)
        {
            case TestSelectionKind.All:
                return TestSelection.All;
            case TestSelectionKind.Names or TestSelectionKind.FrameworkFilter:
                return requested;
        }

        if (requested is { Kind: TestSelectionKind.TestIds, TestIds.Count: 0 })
            return TestSelection.FromTestIds([]);

        var ids = (discovered.Count > 0
                ? discovered.Select(test => test.FullName ?? test.TestId)
                : requested.TestIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0)
            return TestSelection.FromTestIds([]);

        var xml = NUnitCollapsedSelection.ToFilterXml(ids);
        if (string.IsNullOrWhiteSpace(xml))
            return TestSelection.FromTestIds([]);

        return TestSelection.FromFrameworkFilter(
            NUnitSelectionXml.XmlFilterFormat,
            xml!);
    }

    public IReadOnlyList<TestCaseResult> FoldResults(
        TestSelection requested,
        IReadOnlyList<TestDiscoveredTest> discovered,
        IReadOnlyList<TestCaseResult> hostResults)
    {
        switch (requested.Kind)
        {
            case TestSelectionKind.Names:
                return hostResults;
            case TestSelectionKind.TestIds when discovered.Count == 0:
                return FoldUnknown(requested.TestIds, hostResults);
        }

        var index = NUnitIdentityIndex.Build(discovered);
        var expandStub = requested.Kind == TestSelectionKind.TestIds;
        var targets = requested.Kind == TestSelectionKind.TestIds
            ? index.Select(requested.TestIds)
            : discovered;
        var folded = new List<TestCaseResult>();
        var usedHostIds = new HashSet<string>(StringComparer.Ordinal);
        var published = new HashSet<string>(StringComparer.Ordinal);
        foreach (var leaf in targets)
        {
            if (string.IsNullOrWhiteSpace(leaf.TestId) || !published.Add(leaf.TestId))
                continue;
            var matches = hostResults
                .Where(result => expandStub
                    ? index.HostMatches(leaf, result)
                    : index.ExactIdentity(leaf, result))
                .ToList();
            if (matches.Count == 0)
                continue;
            RememberUsed(matches, usedHostIds);
            folded.Add(FoldMatches(leaf.TestId, leaf.DisplayName, matches));
        }

        if (requested.Kind != TestSelectionKind.TestIds)
            folded.AddRange(hostResults.Where(result => !IsUsed(result, usedHostIds)));
        return folded;
    }

    public IReadOnlyList<TestCaseResult> ResultsForUnreported(
        TestSelection requested,
        IReadOnlyList<TestDiscoveredTest> discovered,
        IReadOnlyList<TestCaseResult> hostResults)
    {
        if (requested.Kind != TestSelectionKind.TestIds)
            return [];

        var index = NUnitIdentityIndex.Build(discovered);
        var missing = new List<TestCaseResult>();
        foreach (var id in UnreportedIds(requested, discovered, index))
        {
            if (hostResults.Any(result => HostOrCollapsed(index, discovered, id, result)))
                continue;

            var display = discovered
                .FirstOrDefault(test => string.Equals(test.TestId, id, StringComparison.Ordinal))
                ?.DisplayName ?? id;
            missing.Add(new TestCaseResult(
                id,
                display,
                TestOutcomes.Failed,
                0,
                UnreportedFullNameMessage,
                null,
                null,
                null,
                [],
                []));
        }

        return missing;
    }

    private static IEnumerable<string> UnreportedIds(
        TestSelection requested,
        IReadOnlyList<TestDiscoveredTest> discovered,
        NUnitIdentityIndex index)
    {
        if (discovered.Count > 0)
        {
            var leaves = requested.TestIds.Count > 0 ? index.Select(requested.TestIds) : discovered;
            if (leaves.Count > 0)
                return DistinctIds(leaves.Select(test => test.TestId).ToList());
        }

        return DistinctIds(requested.TestIds);
    }

    private static bool HostOrCollapsed(
        NUnitIdentityIndex index,
        IReadOnlyList<TestDiscoveredTest> discovered,
        string id,
        TestCaseResult result)
    {
        var leaf = discovered.FirstOrDefault(test =>
            string.Equals(test.TestId, id, StringComparison.Ordinal));
        if (leaf is not null)
            return index.HostMatches(leaf, result);
        return NUnitCollapsedSelection.Matches(id, result.TestId, result.FullName, result.ParentTestId);
    }

    private static IReadOnlyList<TestCaseResult> FoldUnknown(
        IReadOnlyList<string> requestedIds,
        IReadOnlyList<TestCaseResult> hostResults)
    {
        var folded = new List<TestCaseResult>();
        foreach (var id in DistinctIds(requestedIds))
        {
            var matches = hostResults
                .Where(result =>
                    NUnitCollapsedSelection.Matches(id, result.TestId, result.FullName, result.ParentTestId))
                .ToList();
            if (matches.Count == 0)
                continue;
            folded.Add(FoldMatches(id, id, matches));
        }

        return folded;
    }

    private static IEnumerable<string> DistinctIds(IReadOnlyList<string>? ids)
    {
        if (ids is null)
            yield break;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in ids)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            var id = value.Trim();
            if (seen.Add(id))
                yield return id;
        }
    }

    private static TestCaseResult FoldMatches(
        string id,
        string displayName,
        IReadOnlyList<TestCaseResult> matches)
    {
        if (matches.Count == 1 && string.Equals(matches[0].TestId, id, StringComparison.Ordinal))
            return matches[0];
        return Collapse(id, displayName, matches);
    }

    private static void RememberUsed(IEnumerable<TestCaseResult> matches, HashSet<string> usedHostIds)
    {
        foreach (var match in matches)
        {
            if (!string.IsNullOrWhiteSpace(match.TestId))
                usedHostIds.Add(match.TestId);
        }
    }

    private static bool IsUsed(TestCaseResult result, HashSet<string> usedHostIds) =>
        !string.IsNullOrWhiteSpace(result.TestId) && usedHostIds.Contains(result.TestId);

    private static TestCaseResult Collapse(
        string testId,
        string displayName,
        IReadOnlyList<TestCaseResult> matches)
    {
        var outcome = WorstOutcome(matches);
        var duration = matches.Sum(result => result.DurationMilliseconds);
        var messages = matches
            .Select(result => result.Message)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();
        var stacks = matches
            .Select(result => result.StackTrace)
            .Where(stack => !string.IsNullOrWhiteSpace(stack))
            .ToList();
        var outputs = matches
            .Select(result => result.Output)
            .Where(output => !string.IsNullOrWhiteSpace(output))
            .ToList();

        return new TestCaseResult(
            testId,
            displayName,
            outcome,
            duration,
            messages.Count == 0 ? null : string.Join(Environment.NewLine, messages),
            stacks.Count == 0 ? null : string.Join(Environment.NewLine, stacks),
            outputs.Count == 0 ? null : string.Join(Environment.NewLine, outputs),
            matches.Select(result => result.Source).FirstOrDefault(source => source is not null),
            matches.SelectMany(result => result.Traits).ToList(),
            matches.SelectMany(result => result.Attachments).ToList(),
            FullName: testId);
    }

    private static string WorstOutcome(IReadOnlyList<TestCaseResult> matches)
    {
        foreach (var outcome in new[]
        {
            TestOutcomes.Error,
            TestOutcomes.Failed,
            TestOutcomes.Cancelled,
            TestOutcomes.Inconclusive,
            TestOutcomes.Skipped,
        })
        {
            if (matches.Any(result => string.Equals(result.Outcome, outcome, StringComparison.Ordinal)))
                return outcome;
        }

        return TestOutcomes.Passed;
    }
}
