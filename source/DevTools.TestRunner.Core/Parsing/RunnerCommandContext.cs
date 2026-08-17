using DevTools.TestRunner.Core.Composition;

namespace DevTools.TestRunner.Core.Parsing;

/// <summary>Framework-neutral command context validated before provider-specific mapping.</summary>
public sealed record RunnerCommandContext(
    string Command,
    string AssemblyPath,
    string Host,
    string Version,
    bool HostLaunch,
    int HostTimeoutSeconds,
    int HostLaunchTimeoutSeconds,
    bool Debug,
    int? DebugParentPid,
    string FrameworkId,
    bool UseGenericProtocol)
{
    public static bool TryCreate(
        RunnerModuleRegistry modules,
        string command,
        string assemblyPath,
        string host,
        string hostVersion,
        bool hostLaunch,
        int hostTimeoutSeconds,
        int hostLaunchTimeoutSeconds,
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

        var useGenericProtocol = !string.IsNullOrWhiteSpace(framework);
        string frameworkId;
        try
        {
            frameworkId = useGenericProtocol
                ? RunnerModuleRegistry.NormalizeFrameworkId(framework!)
                : modules.GetDefaultFrameworkId();
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
        if (!modules.TrySelect(["--framework", frameworkId], out _, out error))
            return false;

        context = new RunnerCommandContext(
            command,
            Path.GetFullPath(assemblyPath),
            host.Trim(),
            hostVersion.Trim(),
            hostLaunch,
            hostTimeoutSeconds,
            hostLaunchTimeoutSeconds,
            debug || debugParentPid is not null,
            debugParentPid,
            frameworkId,
            useGenericProtocol);
        return true;
    }
}
