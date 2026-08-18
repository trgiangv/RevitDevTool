namespace DevTools.NUnit.Discovery;

/// <summary>
/// NUnit-local discovery selection shared by the MTP framework and TestRunner command module.
/// </summary>
public readonly record struct NUnitDiscoveryFilter(
    IReadOnlyList<string> Names,
    IReadOnlyList<string> FullNames)
{
    public static NUnitDiscoveryFilter Empty { get; } = new([], []);

    public bool IsEmpty => Names.Count == 0 && FullNames.Count == 0;

    public static NUnitDiscoveryFilter FromNames(params string[] names) =>
        new(Clean(names), []);

    public static NUnitDiscoveryFilter FromFullNames(params string[] fullNames) =>
        new([], Clean(fullNames));

    public static NUnitDiscoveryFilter FromFullNames(IEnumerable<string> fullNames) =>
        new([], Clean(fullNames));

    private static IReadOnlyList<string> Clean(IEnumerable<string> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
