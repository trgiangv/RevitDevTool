using DevTools.NUnit.Runtime;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP;

internal sealed partial class NUnitHostTestDiscoverer
{
    internal const string UnreportedFullNameMessage =
        "Host NUnit did not report this FullName. UID is ITest.FullName from testhost ExploreTests; in-host source expansion uses a different FullName.";

    public TestingSelection ToHostSelection(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered)
    {
        if (!IsConstrained(requested))
            return requested;

        if (IsNamesOnly(requested))
            return requested;

        var ids = (requested.TestIds ?? [])
            .Select(id => ToHostFullName(id, discovered))
            .Concat(discovered.Select(test => test.FullName ?? test.TestId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0)
            return requested;

        return new TestingSelection([], NUnitCollapsedSelection.ToFilterXml(ids));
    }

    public IReadOnlyList<TestingCaseResult> FoldResults(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults)
    {
        if (IsNamesOnly(requested))
            return hostResults;

        var display = DisplayNames(discovered);
        var folded = new List<TestingCaseResult>();
        var usedHostIds = new HashSet<string>(StringComparer.Ordinal);
        FoldRequestedIds(requested.TestIds, discovered, hostResults, display, folded, usedHostIds);
        FoldDiscoveredLeaves(discovered, hostResults, folded, usedHostIds);
        if (!HasIds(requested.TestIds))
            AppendUnusedHostResults(hostResults, folded, usedHostIds);
        return folded;
    }

    private static bool IsNamesOnly(TestingSelection requested) =>
        !HasIds(requested.TestIds) && HasIds(requested.Names);

    private static bool HasIds(IReadOnlyList<string>? ids) => ids is { Count: > 0 };

    private static Dictionary<string, string> DisplayNames(IReadOnlyList<TestingDiscoveredTest> discovered) =>
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
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults,
        IReadOnlyDictionary<string, string> display,
        List<TestingCaseResult> folded,
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

    private static List<TestingCaseResult> HostMatches(
        string id,
        string hostId,
        IReadOnlyList<TestingCaseResult> hostResults)
    {
        var matches = new List<TestingCaseResult>();
        foreach (var result in hostResults)
        {
            if (MatchesCollapsed(id, result) || MatchesCollapsed(hostId, result))
                matches.Add(result);
        }

        return matches;
    }

    private static bool MatchesCollapsed(string id, TestingCaseResult result) =>
        NUnitCollapsedSelection.Matches(id, result.TestId, result.FullName, result.ParentTestId);

    private static TestingCaseResult FoldMatches(
        string id,
        string displayName,
        IReadOnlyList<TestingCaseResult> matches)
    {
        if (matches.Count == 1 && string.Equals(matches[0].TestId, id, StringComparison.Ordinal))
            return matches[0];
        return Collapse(id, displayName, matches);
    }

    private static void FoldDiscoveredLeaves(
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults,
        List<TestingCaseResult> folded,
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
        TestingDiscoveredTest test,
        HashSet<string> published,
        out string id)
    {
        id = string.IsNullOrWhiteSpace(test.TestId) ? string.Empty : test.TestId.Trim();
        return id.Length > 0 && !published.Contains(id);
    }

    private static TestingCaseResult? FindExactHostResult(
        TestingDiscoveredTest test,
        string id,
        IReadOnlyList<TestingCaseResult> hostResults)
    {
        foreach (var result in hostResults)
        {
            if (SameIdentity(result, id, test.FullName))
                return result;
        }

        return null;
    }

    private static bool SameIdentity(TestingCaseResult result, string id, string? fullName) =>
        string.Equals(result.TestId, id, StringComparison.Ordinal)
        || string.Equals(result.FullName, id, StringComparison.Ordinal)
        || string.Equals(result.TestId, fullName, StringComparison.Ordinal)
        || string.Equals(result.FullName, fullName, StringComparison.Ordinal);

    private static TestingCaseResult FoldOnto(string id, string displayName, TestingCaseResult match) =>
        string.Equals(match.TestId, id, StringComparison.Ordinal)
            ? match
            : Collapse(id, displayName, [match]);

    private static void RememberUsed(TestingCaseResult match, HashSet<string> usedHostIds)
    {
        if (!string.IsNullOrWhiteSpace(match.TestId))
            usedHostIds.Add(match.TestId);
    }

    private static void RememberUsed(IEnumerable<TestingCaseResult> matches, HashSet<string> usedHostIds)
    {
        foreach (var match in matches)
            RememberUsed(match, usedHostIds);
    }

    private static void AppendUnusedHostResults(
        IReadOnlyList<TestingCaseResult> hostResults,
        List<TestingCaseResult> folded,
        HashSet<string> usedHostIds)
    {
        foreach (var result in hostResults)
        {
            if (IsUsed(result, usedHostIds))
                continue;
            folded.Add(result);
        }
    }

    private static bool IsUsed(TestingCaseResult result, HashSet<string> usedHostIds) =>
        !string.IsNullOrWhiteSpace(result.TestId) && usedHostIds.Contains(result.TestId);

    public IReadOnlyList<TestingCaseResult> ResultsForUnreported(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults)
    {
        if (!HasIds(requested.TestIds))
            return [];

        var reported = new HashSet<string>(
            hostResults.Select(result => result.TestId),
            StringComparer.Ordinal);
        var display = DisplayNames(discovered);
        var missing = new List<TestingCaseResult>();
        foreach (var id in DistinctIds(requested.TestIds))
        {
            if (reported.Contains(id))
                continue;

            missing.Add(new TestingCaseResult(
                id,
                display.TryGetValue(id, out var displayName) ? displayName : id,
                TestingOutcomes.Failed,
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

    private static bool IsConstrained(TestingSelection selection) =>
        HasIds(selection.TestIds) || HasIds(selection.Names);

    private static string ToHostFullName(string id, IReadOnlyList<TestingDiscoveredTest> discovered)
    {
        foreach (var test in discovered)
        {
            if (string.Equals(test.TestId, id, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(test.FullName))
                return test.FullName!;
        }

        return id;
    }

    private static TestingCaseResult Collapse(
        string testId,
        string displayName,
        IReadOnlyList<TestingCaseResult> matches)
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

        return new TestingCaseResult(
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

    private static string WorstOutcome(IReadOnlyList<TestingCaseResult> matches)
    {
        foreach (var outcome in new[]
        {
            TestingOutcomes.Error,
            TestingOutcomes.Failed,
            TestingOutcomes.Cancelled,
            TestingOutcomes.Inconclusive,
            TestingOutcomes.Skipped,
        })
        {
            if (matches.Any(result => string.Equals(result.Outcome, outcome, StringComparison.Ordinal)))
                return outcome;
        }

        return TestingOutcomes.Passed;
    }
}
