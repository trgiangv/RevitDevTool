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

public interface IRunnerClient
{
    IReadOnlyList<RemoteTestCase> Discover(string source, RunnerHostOptions options);

    RemoteRunResult Run(
        string source,
        string? filter,
        RunnerHostOptions options,
        bool waitForDebugger);

    void Cancel();
}

public sealed record RunnerHostOptions(
    string Host,
    string HostVersion,
    bool HostLaunch,
    int HostTimeoutSeconds,
    int HostLaunchTimeoutSeconds);
