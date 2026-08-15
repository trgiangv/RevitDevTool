using DevTools.NUnit.Runner.Services;

namespace DevTools.NUnit.Runner.Parsing;

public sealed record RunnerCommandLine(
    string Command,
    string AssemblyPath,
    string Host,
    string Version,
    string? Filter,
    bool HostLaunch,
    int HostTimeoutSeconds,
    int HostLaunchTimeoutSeconds,
    bool Debug = false,
    int? DebugParentPid = null)
{
    public static bool TryCreate(
        string command,
        string assemblyPath,
        string host,
        string hostVersion,
        IReadOnlyList<string>? names,
        IReadOnlyList<string>? tests,
        string? filterXml,
        bool hostLaunch,
        int hostTimeoutSeconds,
        int hostLaunchTimeoutSeconds,
        bool debug,
        int? debugParentPid,
        out RunnerCommandLine? options,
        out string? error)
    {
        options = null;
        error = null;

        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            error = "Assembly path is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(host))
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

        if (!NUnitRunnerFilter.TryCompose(names, tests, filterXml, out var filter, out error))
            return false;

        var parentPid = debugParentPid;
        options = new RunnerCommandLine(
            command,
            Path.GetFullPath(assemblyPath),
            host.Trim(),
            hostVersion.Trim(),
            filter,
            hostLaunch,
            hostTimeoutSeconds,
            hostLaunchTimeoutSeconds,
            debug || parentPid is not null,
            parentPid);
        return true;
    }
}
