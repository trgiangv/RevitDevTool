namespace DevTools.NUnit.Provider;

/// <summary>
/// Runner <c>--name</c> / <c>--test</c> selection. Shared by MTP and VSTest.
/// </summary>
public readonly record struct RunnerTestFilter(
    IReadOnlyList<string> Names,
    IReadOnlyList<string> FullNames)
{
    public static RunnerTestFilter Empty { get; } = new([], []);

    public bool IsEmpty => Names.Count == 0 && FullNames.Count == 0;

    public static RunnerTestFilter FromNames(params string[] names) =>
        new(Clean(names), []);

    public static RunnerTestFilter FromFullNames(params string[] fullNames) =>
        new([], Clean(fullNames));

    public static RunnerTestFilter FromFullNames(IEnumerable<string> fullNames) =>
        new([], Clean(fullNames));

    private static IReadOnlyList<string> Clean(IEnumerable<string> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
