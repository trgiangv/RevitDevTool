using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using DevTools.Testing.Abstractions.Contracts;
// ReSharper disable RedundantSuppressNullableWarningExpression
#pragma warning disable TPEXP

namespace DevTools.TestAdapter;

/// <summary>
/// Turns MTP execution filters (uid list, tree-node, <c>--filter</c> name) into
/// a neutral <see cref="TestSelection"/>. Pure functions of the request; the
/// discoverer and host are involved only by the caller.
/// </summary>
internal static class TestExecutionFilters
{
    internal static TestSelection ToRunnerFilter(
        ITestExecutionFilter? filter,
        string? nameFilter = null)
    {
        var uids = CollectUidList(filter);
        if (HasUidListFilter(filter))
            return TestSelection.FromTestIds(uids);

        return string.IsNullOrWhiteSpace(nameFilter) 
            ? TestSelection.All 
            : TestSelection.FromNames([nameFilter!.Trim()]);
    }

    /// <summary>
    /// Visual Studio / Rider may send an empty <see cref="TestNodeUidListFilter"/>
    /// on discover-all. That is not a run of zero tests — publish the assembly.
    /// </summary>
    internal static TestSelection ToDiscoverFilter(
        ITestExecutionFilter? filter,
        string? nameFilter = null)
    {
        var selection = ToRunnerFilter(filter, nameFilter);
        return selection is { Kind: TestSelectionKind.TestIds, TestIds.Count: 0 } 
            ? TestSelection.All 
            : selection;
    }

    private static bool HasUidListFilter(ITestExecutionFilter? filter) =>
        filter switch
        {
            TestNodeUidListFilter => true,
            CompositeTestExecutionFilter composite => composite.Filters.Any(HasUidListFilter),
            _ => false,
        };

    private static List<string> CollectUidList(ITestExecutionFilter? filter)
    {
        var uids = new List<string>();
        CollectUidList(filter, uids);
        return uids
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void CollectUidList(ITestExecutionFilter? filter, List<string> uids)
    {
        switch (filter)
        {
            case TestNodeUidListFilter uidFilter:
                foreach (var uid in uidFilter.TestNodeUids)
                    uids.Add(uid.Value);
                break;
            case CompositeTestExecutionFilter composite:
                foreach (var child in composite.Filters)
                    CollectUidList(child, uids);
                break;
        }
    }

    internal static bool HasTreeNodeFilter(ITestExecutionFilter? filter) =>
        FindTreeNodeFilter(filter) is not null;

    internal static TestSelection ExpandTreeFilter(
        ITestExecutionFilter? filter,
        IReadOnlyList<TestDiscoveredTest> all)
    {
        var tree = FindTreeNodeFilter(filter);
        if (tree is null)
            return TestSelection.All;

        var ids = all
            .Where(test => MatchesTree(tree, test))
            .Select(test => test.TestId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return TestSelection.FromTestIds(ids);
    }

    private static TreeNodeFilter? FindTreeNodeFilter(ITestExecutionFilter? filter) =>
        filter switch
        {
            TreeNodeFilter tree => tree,
            CompositeTestExecutionFilter composite => composite.Filters
                .Select(FindTreeNodeFilter)
                .FirstOrDefault(child => child is not null),
            _ => null,
        };

    internal static bool MatchesTree(TreeNodeFilter tree, TestDiscoveredTest test)
    {
        var bag = new PropertyBag();
        foreach (var path in MtpTreePaths(test))
        {
            if (tree.MatchesFilter(path, bag))
                return true;
        }

        return false;
    }

    internal static IEnumerable<string> MtpTreePaths(TestDiscoveredTest test)
    {
        yield return "/" + Uri.EscapeDataString(test.TestId);
        if (!string.IsNullOrWhiteSpace(test.DisplayName))
            yield return "/" + Uri.EscapeDataString(test.DisplayName);
        if (string.IsNullOrWhiteSpace(test.Namespace)
            || string.IsNullOrWhiteSpace(test.TypeName)
            || string.IsNullOrWhiteSpace(test.MethodName))
            yield break;

        yield return string.Concat(
            "/", Uri.EscapeDataString(test.Namespace!),
            "/", Uri.EscapeDataString(test.TypeName!),
            "/", Uri.EscapeDataString(test.MethodName!));
        if (!string.IsNullOrWhiteSpace(test.DisplayName))
        {
            yield return string.Concat(
                "/", Uri.EscapeDataString(test.Namespace!),
                "/", Uri.EscapeDataString(test.TypeName!),
                "/", Uri.EscapeDataString(test.DisplayName));
        }
    }
}
