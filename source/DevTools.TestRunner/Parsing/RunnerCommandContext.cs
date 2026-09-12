namespace DevTools.TestRunner.Parsing;

/// <summary>Validated host execution context.</summary>
public sealed record RunnerCommandContext(
    string HostName,
    string HostVersion,
    bool ForceLaunch,
    int PerTestTimeoutSeconds,
    int LaunchTimeoutSeconds,
    bool Debug,
    int? DebugParentPid,
    int RequestTimeoutSeconds = 0)
{
    public int EffectiveRequestTimeoutSeconds =>
        RequestTimeoutSeconds > 0 ? RequestTimeoutSeconds : PerTestTimeoutSeconds;

    public static bool TryCreate(
        string hostName,
        string hostVersion,
        bool forceLaunch,
        int perTestTimeoutSeconds,
        int launchTimeoutSeconds,
        bool debug,
        int? debugParentPid,
        int requestTimeoutSeconds,
        out RunnerCommandContext? context,
        out string? error)
    {
        context = null;
        error = null;
        if (string.IsNullOrWhiteSpace(hostName))
        {
            error = "Host name is required.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(hostVersion))
        {
            error = "Host version is required.";
            return false;
        }
        if (debugParentPid is <= 0)
        {
            error = "Debug parent pid requires a positive process id.";
            return false;
        }

        if (perTestTimeoutSeconds <= 0)
        {
            error = "Per-test timeout must be positive.";
            return false;
        }

        if (launchTimeoutSeconds <= 0)
        {
            error = "Launch timeout must be positive.";
            return false;
        }

        if (requestTimeoutSeconds < 0)
        {
            error = "Request timeout cannot be negative.";
            return false;
        }

        context = new RunnerCommandContext(
            hostName.Trim(),
            hostVersion.Trim(),
            forceLaunch,
            perTestTimeoutSeconds,
            launchTimeoutSeconds,
            debug || debugParentPid is not null,
            debugParentPid,
            requestTimeoutSeconds);
        return true;
    }
}
