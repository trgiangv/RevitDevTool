using DevTools.NUnit.Core.Contracts;

namespace DevTools.NUnit.Mtp;

internal readonly record struct RunnerTestFilter(
    IReadOnlyList<string> Names,
    IReadOnlyList<string> FullNames)
{
    internal static RunnerTestFilter Empty { get; } = new([], []);

    internal bool IsEmpty => Names.Count == 0 && FullNames.Count == 0;

    internal static RunnerTestFilter FromNames(params string[] names) =>
        new(Clean(names), []);

    internal static RunnerTestFilter FromFullNames(params string[] fullNames) =>
        new([], Clean(fullNames));

    private static IReadOnlyList<string> Clean(IEnumerable<string> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
}

internal interface IRunnerTransport
{
    IReadOnlyList<NUnitDiscoveredTest> Discover(
        string assemblyPath,
        HostRunOptions options,
        RunnerTestFilter filter);

    IReadOnlyList<NUnitCaseResult> Run(
        string assemblyPath,
        HostRunOptions options,
        RunnerTestFilter filter);

    void Cancel();
}
