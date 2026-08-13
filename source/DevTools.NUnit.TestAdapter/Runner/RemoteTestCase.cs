namespace DevTools.NUnit.TestAdapter.Runner;

public sealed record RemoteTestCase(string Id, string Name, string FullName, string Source);

public sealed record RemoteRunResult(IReadOnlyList<RemoteTestCaseResult> Cases);

public sealed record RemoteTestCaseResult(
    string Name,
    string Outcome,
    double DurationMilliseconds,
    string? Message,
    string? StackTrace,
    string? Output);

public readonly record struct RunnerTestFilter(
    IReadOnlyList<string> Names,
    IReadOnlyList<string> FullNames)
{
    public static RunnerTestFilter Empty { get; } = new([], []);

    public static RunnerTestFilter FromFullNames(IEnumerable<string> fullNames) =>
        new([], Clean(fullNames));

    private static IReadOnlyList<string> Clean(IEnumerable<string> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
}

public interface IRunnerClient
{
    IReadOnlyList<RemoteTestCase> Discover(
        string assemblyPath,
        RunnerHostOptions options,
        RunnerTestFilter filter);

    RemoteRunResult Run(
        string assemblyPath,
        RunnerHostOptions options,
        RunnerTestFilter filter);

    void Cancel();
}

public sealed record RunnerHostOptions(
    string Host,
    string HostVersion,
    bool HostLaunch,
    int HostTimeoutSeconds,
    int HostLaunchTimeoutSeconds,
    string? RunnerPath);
