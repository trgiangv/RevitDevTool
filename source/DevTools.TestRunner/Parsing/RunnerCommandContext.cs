using DevTools.Testing.Abstractions.Contracts;
namespace DevTools.TestRunner.Parsing;

/// <summary>Validated run context. <see cref="FrameworkId"/> is already parsed from user config.</summary>
public sealed record RunnerCommandContext(
    string AssemblyPath,
    string HostName,
    string HostVersion,
    bool ForceLaunch,
    int PerTestTimeoutSeconds,
    int LaunchTimeoutSeconds,
    bool Debug,
    int? DebugParentPid,
    TestFrameworkId FrameworkId,
    int RequestTimeoutSeconds = 0)
{
    public int EffectiveRequestTimeoutSeconds =>
        RequestTimeoutSeconds > 0 ? RequestTimeoutSeconds : PerTestTimeoutSeconds;

    public static bool TryCreate(
        string assemblyPath,
        string hostName,
        string hostVersion,
        bool forceLaunch,
        int perTestTimeoutSeconds,
        int launchTimeoutSeconds,
        bool debug,
        int? debugParentPid,
        TestFrameworkId framework,
        int requestTimeoutSeconds,
        out RunnerCommandContext? context,
        out string? error)
    {
        context = null;
        error = null;
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            error = "Assembly path is required.";
            return false;
        }
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

        if (!Enum.IsDefined(framework))
        {
            error = "Framework id is required.";
            return false;
        }

        context = new RunnerCommandContext(
            Path.GetFullPath(assemblyPath),
            hostName.Trim(),
            hostVersion.Trim(),
            forceLaunch,
            perTestTimeoutSeconds,
            launchTimeoutSeconds,
            debug || debugParentPid is not null,
            debugParentPid,
            framework,
            requestTimeoutSeconds);
        return true;
    }
}
