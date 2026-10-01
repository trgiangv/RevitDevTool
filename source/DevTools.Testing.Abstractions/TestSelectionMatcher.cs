using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions;

/// <summary>
/// The one rule testhost discoverers use to pick tests out of a framework listing:
/// <see cref="TestSelectionKind.TestIds"/> match by uid or full name,
/// <see cref="TestSelectionKind.Names"/> by full name, display name, method name or a
/// case-insensitive substring. A framework filter selects nothing here; mappers own it.
/// </summary>
public sealed class TestSelectionMatcher
{
    private readonly bool _all;
    private readonly HashSet<string> _ids;
    private readonly HashSet<string> _names;

    private TestSelectionMatcher(bool all, HashSet<string> ids, HashSet<string> names)
    {
        _all = all;
        _ids = ids;
        _names = names;
    }

    /// <summary>True when nothing can match (empty ids/names, or a framework filter).</summary>
    public bool SelectsNothing => !_all && _ids.Count == 0 && _names.Count == 0;

    public static TestSelectionMatcher For(TestSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return selection.Kind switch
        {
            TestSelectionKind.All => new TestSelectionMatcher(true, [], []),
            TestSelectionKind.TestIds => new TestSelectionMatcher(false, Clean(selection.TestIds), []),
            TestSelectionKind.Names => new TestSelectionMatcher(false, [], Clean(selection.Names)),
            _ => new TestSelectionMatcher(false, [], []),
        };
    }

    /// <param name="test">The discovered test.</param>
    /// <param name="methodName">Overrides <see cref="TestDiscoveredTest.MethodName"/> for name matching.</param>
    /// <param name="alternateId">A second uid the framework also accepts for this test; only evaluated when ids are being matched.</param>
    public bool Matches(TestDiscoveredTest test, string? methodName = null, Func<string?>? alternateId = null)
    {
        ArgumentNullException.ThrowIfNull(test);
        if (_all)
            return true;
        if (SelectsNothing)
            return false;

        if (_ids.Contains(test.TestId)
            || (!string.IsNullOrWhiteSpace(test.FullName) && _ids.Contains(test.FullName!))
            || (_ids.Count > 0 && alternateId?.Invoke() is { } alternate && _ids.Contains(alternate)))
        {
            return true;
        }

        var method = methodName ?? test.MethodName;
        return _names.Any(name =>
            string.Equals(test.FullName, name, StringComparison.Ordinal)
            || string.Equals(test.DisplayName, name, StringComparison.Ordinal)
            || (test.FullName?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false)
            || test.TestId.Contains(name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, name, StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> Clean(IReadOnlyList<string>? values) =>
        values is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : values.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToHashSet(StringComparer.Ordinal);
}
