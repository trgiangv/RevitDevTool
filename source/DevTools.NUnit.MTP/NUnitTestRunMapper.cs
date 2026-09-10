using DevTools.NUnit.Runtime;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP;

/// <summary>
/// Maps testhost NUnit identities onto in-host filter XML and folds host
/// results back onto IDE test-node ids. Discovery stays on
/// <see cref="NUnitTestDiscoverer"/>.
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

        var ids = requested.TestIds
            .Select(id => ToHostFullName(id, discovered))
            .Concat(discovered.Select(test => test.FullName ?? test.TestId))
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
        if (requested.Kind == TestSelectionKind.Names)
            return hostResults;

        var display = DisplayNames(discovered);
        var folded = new List<TestCaseResult>();
        var usedHostIds = new HashSet<string>(StringComparer.Ordinal);
        FoldRequestedIds(requested.TestIds, discovered, hostResults, display, folded, usedHostIds);
        FoldDiscoveredLeaves(discovered, hostResults, folded, usedHostIds);
        if (requested.Kind != TestSelectionKind.TestIds)
            AppendUnusedHostResults(hostResults, folded, usedHostIds);
        return folded;
    }

    public IReadOnlyList<TestCaseResult> ResultsForUnreported(
        TestSelection requested,
        IReadOnlyList<TestDiscoveredTest> discovered,
        IReadOnlyList<TestCaseResult> hostResults)
    {
        if (requested.Kind != TestSelectionKind.TestIds)
            return [];

        var reported = new HashSet<string>(
            hostResults.Select(result => result.TestId),
            StringComparer.Ordinal);
        var display = DisplayNames(discovered);
        var missing = new List<TestCaseResult>();
        foreach (var id in DistinctIds(requested.TestIds))
        {
            if (reported.Contains(id))
                continue;

            missing.Add(new TestCaseResult(
                id,
                display.GetValueOrDefault(id, id),
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

    private static bool HasIds(IReadOnlyList<string>? ids) => ids is { Count: > 0 };

    private static Dictionary<string, string> DisplayNames(IReadOnlyList<TestDiscoveredTest> discovered) =>
        discovered
            .Where(test => !string.IsNullOrWhiteSpace(test.TestId))
            .GroupBy(test => test.TestId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().DisplayName, StringComparer.Ordinal);

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

    private static void FoldRequestedIds(
        IReadOnlyList<string>? requestedIds,
        IReadOnlyList<TestDiscoveredTest> discovered,
        IReadOnlyList<TestCaseResult> hostResults,
        IReadOnlyDictionary<string, string> display,
        List<TestCaseResult> folded,
        HashSet<string> usedHostIds)
    {
        if (!HasIds(requestedIds))
            return;

        foreach (var id in DistinctIds(requestedIds))
        {
            var matches = HostMatches(id, ToHostFullName(id, discovered), hostResults);
            if (matches.Count == 0)
                continue;

            RememberUsed(matches, usedHostIds);
            folded.Add(FoldMatches(id, display.TryGetValue(id, out var name) ? name : id, matches));
        }
    }

    private static List<TestCaseResult> HostMatches(
        string id,
        string hostId,
        IReadOnlyList<TestCaseResult> hostResults)
    {
        return hostResults.Where(result => MatchesCollapsed(id, result) || MatchesCollapsed(hostId, result)).ToList();
    }

    private static bool MatchesCollapsed(string id, TestCaseResult result) =>
        NUnitCollapsedSelection.Matches(id, result.TestId, result.FullName, result.ParentTestId);

    private static TestCaseResult FoldMatches(
        string id,
        string displayName,
        IReadOnlyList<TestCaseResult> matches)
    {
        if (matches.Count == 1 && string.Equals(matches[0].TestId, id, StringComparison.Ordinal))
            return matches[0];
        return Collapse(id, displayName, matches);
    }

    private static void FoldDiscoveredLeaves(
        IReadOnlyList<TestDiscoveredTest> discovered,
        IReadOnlyList<TestCaseResult> hostResults,
        List<TestCaseResult> folded,
        HashSet<string> usedHostIds)
    {
        var published = new HashSet<string>(folded.Select(result => result.TestId), StringComparer.Ordinal);
        foreach (var test in discovered)
        {
            if (!TryUnpublishedId(test, published, out var id))
                continue;

            var match = FindExactHostResult(test, id, hostResults);
            if (match is null)
                continue;

            RememberUsed(match, usedHostIds);
            folded.Add(FoldOnto(id, test.DisplayName, match));
            published.Add(id);
        }
    }

    private static bool TryUnpublishedId(
        TestDiscoveredTest test,
        HashSet<string> published,
        out string id)
    {
        id = string.IsNullOrWhiteSpace(test.TestId) ? string.Empty : test.TestId.Trim();
        return id.Length > 0 && !published.Contains(id);
    }

    private static TestCaseResult? FindExactHostResult(
        TestDiscoveredTest test,
        string id,
        IReadOnlyList<TestCaseResult> hostResults)
    {
        return hostResults.FirstOrDefault(result => SameIdentity(result, id, test.FullName));
    }

    private static bool SameIdentity(TestCaseResult result, string id, string? fullName) =>
        string.Equals(result.TestId, id, StringComparison.Ordinal)
        || string.Equals(result.FullName, id, StringComparison.Ordinal)
        || string.Equals(result.TestId, fullName, StringComparison.Ordinal)
        || string.Equals(result.FullName, fullName, StringComparison.Ordinal);

    private static TestCaseResult FoldOnto(string id, string displayName, TestCaseResult match) =>
        string.Equals(match.TestId, id, StringComparison.Ordinal)
            ? match
            : Collapse(id, displayName, [match]);

    private static void RememberUsed(TestCaseResult match, HashSet<string> usedHostIds)
    {
        if (!string.IsNullOrWhiteSpace(match.TestId))
            usedHostIds.Add(match.TestId);
    }

    private static void RememberUsed(IEnumerable<TestCaseResult> matches, HashSet<string> usedHostIds)
    {
        foreach (var match in matches)
            RememberUsed(match, usedHostIds);
    }

    private static void AppendUnusedHostResults(
        IReadOnlyList<TestCaseResult> hostResults,
        List<TestCaseResult> folded,
        HashSet<string> usedHostIds)
    {
        folded.AddRange(hostResults.Where(result => !IsUsed(result, usedHostIds)));
    }

    private static bool IsUsed(TestCaseResult result, HashSet<string> usedHostIds) =>
        !string.IsNullOrWhiteSpace(result.TestId) && usedHostIds.Contains(result.TestId);

    private static string ToHostFullName(string id, IReadOnlyList<TestDiscoveredTest> discovered)
    {
        foreach (var test in discovered)
        {
            if (string.Equals(test.TestId, id, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(test.FullName))
                return test.FullName!;
        }

        return id;
    }

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
