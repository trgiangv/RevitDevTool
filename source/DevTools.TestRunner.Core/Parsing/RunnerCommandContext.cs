namespace DevTools.TestRunner.Core.Parsing;

/// <summary>Validated CLI context. Framework id is an opaque host-engine token from the adapter.</summary>
public sealed record RunnerCommandContext(
    string Command,
    string AssemblyPath,
    string HostName,
    string HostVersion,
    bool ForceLaunch,
    int PerTestTimeoutSeconds,
    int LaunchTimeoutSeconds,
    bool Debug,
    int? DebugParentPid,
    string FrameworkId)
{
    public static bool TryCreate(
        string command,
        string assemblyPath,
        string hostName,
        string hostVersion,
        bool forceLaunch,
        int perTestTimeoutSeconds,
        int launchTimeoutSeconds,
        bool debug,
        int? debugParentPid,
        string? framework,
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
            error = "--host is required.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(hostVersion))
        {
            error = "--host-version is required.";
            return false;
        }
        if (debugParentPid is <= 0)
        {
            error = "--debug-parent-pid requires a positive process id.";
            return false;
        }

        string frameworkId;
        try
        {
            frameworkId = NormalizeFrameworkId(framework ?? string.Empty);
        }
        catch (ArgumentException)
        {
            error = "--framework is required.";
            return false;
        }

        context = new RunnerCommandContext(
            command,
            Path.GetFullPath(assemblyPath),
            hostName.Trim(),
            hostVersion.Trim(),
            forceLaunch,
            perTestTimeoutSeconds,
            launchTimeoutSeconds,
            debug || debugParentPid is not null,
            debugParentPid,
            frameworkId);
        return true;
    }

    public static string NormalizeFrameworkId(string frameworkId)
    {
        if (string.IsNullOrWhiteSpace(frameworkId))
            throw new ArgumentException("Framework ID is required.", nameof(frameworkId));
        return frameworkId.Trim().ToLowerInvariant();
    }
}
