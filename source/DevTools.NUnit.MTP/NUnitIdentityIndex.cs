using DevTools.NUnit.Runtime;
using DevTools.Testing.Abstractions.Contracts;
using static DevTools.NUnit.Runtime.NUnitNameSyntax;

namespace DevTools.NUnit.MTP;

internal enum NUnitIdentityKind
{
    Leaf,
    Group,
    Unknown,
}

internal readonly struct NUnitIdentityHit(NUnitIdentityKind kind, IReadOnlyList<TestDiscoveredTest> leaves)
{
    public NUnitIdentityKind Kind { get; } = kind;

    public IReadOnlyList<TestDiscoveredTest> Leaves { get; } = leaves;

    public static NUnitIdentityHit Unknown { get; } = new(NUnitIdentityKind.Unknown, []);

    public static NUnitIdentityHit Leaf(TestDiscoveredTest test) =>
        new(NUnitIdentityKind.Leaf, [test]);

    public static NUnitIdentityHit Group(IReadOnlyList<TestDiscoveredTest> leaves) =>
        new(NUnitIdentityKind.Group, leaves);
}

/// <summary>
/// Tree of identities this testhost published. Inbound IDE uids resolve by
/// ordinal lookup of those strings (and their canon aliases). Grouping uids
/// (<c>Class.Method</c> / <c>Class.Method()</c>) are not stored as exact keys —
/// they expand from the method group map. Unknown uids stay unknown.
/// </summary>
internal sealed class NUnitIdentityIndex
{
    private readonly Dictionary<string, TestDiscoveredTest> _exact =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<TestDiscoveredTest>> _groups =
        new(StringComparer.Ordinal);

    private NUnitIdentityIndex(IReadOnlyList<TestDiscoveredTest> leaves)
    {
        Leaves = leaves;
        foreach (var test in leaves)
            Index(test);
    }

    public IReadOnlyList<TestDiscoveredTest> Leaves { get; }

    public static NUnitIdentityIndex Build(IReadOnlyList<TestDiscoveredTest>? leaves) =>
        new(leaves ?? []);

    public IReadOnlyList<TestDiscoveredTest> Select(IReadOnlyList<string> uids)
    {
        var selected = new List<TestDiscoveredTest>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var uid in Distinct(uids))
        {
            selected.AddRange(Resolve(uid).Leaves.Where(leaf => seen.Add(leaf.TestId)));
        }

        return selected;
    }

    public NUnitIdentityHit Resolve(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return NUnitIdentityHit.Unknown;

        uid = uid.Trim();
        var canon = Canonicalize(uid);
        if (TryExact(uid, out var hit) || TryExact(canon, out hit))
            return NUnitIdentityHit.Leaf(hit);

        return _groups.TryGetValue(NUnitTestNameParser.GroupKey(uid), out var grouped)
            ? NUnitIdentityHit.Group(grouped)
            : NUnitIdentityHit.Unknown;
    }

    public bool ExactIdentity(TestDiscoveredTest leaf, TestCaseResult result) =>
        Identifies(leaf.TestId, leaf.FullName, result.TestId)
        || Identifies(leaf.TestId, leaf.FullName, result.FullName);

    public bool HostMatches(TestDiscoveredTest leaf, TestCaseResult result) =>
        ExactIdentity(leaf, result)
        || NUnitCollapsedSelection.Matches(leaf.TestId, result.TestId, result.FullName, result.ParentTestId)
        || (leaf.FullName is { Length: > 0 }
            && NUnitCollapsedSelection.Matches(leaf.FullName, result.TestId, result.FullName, result.ParentTestId));

    private void Index(TestDiscoveredTest test)
    {
        if (string.IsNullOrWhiteSpace(test.TestId))
            return;

        AddExact(test.TestId, test);
        AddExact(test.FullName, test);
        if (test.DisplayName is { Length: > 0 } && ContainsAtDepthZero(test.DisplayName, ArgOpen))
            AddExact(test.DisplayName, test);

        AddGroup(NUnitTestNameParser.GroupKey(
            test.TestId, test.MethodName, test.TypeName, test.Namespace), test);
    }

    private bool TryExact(string uid, out TestDiscoveredTest test) =>
        _exact.TryGetValue(uid, out test!);

    private void AddExact(string? key, TestDiscoveredTest test)
    {
        if (key is not { Length: > 0 })
            return;
        var trimmed = key.Trim();
        if (!_exact.ContainsKey(trimmed))
            _exact[trimmed] = test;
        var canon = Canonicalize(trimmed);
        if (!string.Equals(canon, trimmed, StringComparison.Ordinal) && !_exact.ContainsKey(canon))
            _exact[canon] = test;
    }

    private void AddGroup(string key, TestDiscoveredTest test)
    {
        if (!_groups.TryGetValue(key, out var leaves))
        {
            leaves = [];
            _groups[key] = leaves;
        }

        if (leaves.All(existing => !string.Equals(existing.TestId, test.TestId, StringComparison.Ordinal)))
            leaves.Add(test);
    }

    private static IEnumerable<string> Distinct(IReadOnlyList<string>? values)
    {
        if (values is null)
            yield break;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            var id = value.Trim();
            if (seen.Add(id))
                yield return id;
        }
    }
}
