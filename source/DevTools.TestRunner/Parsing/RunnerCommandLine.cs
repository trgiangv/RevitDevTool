using DevTools.Testing.Abstractions.Contracts;
using DevTools.TestRunner.Services;

namespace DevTools.TestRunner.Parsing;

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
    int? DebugParentPid = null,
    string FrameworkId = TestingFrameworkIds.NUnit,
    bool UseGenericProtocol = false)
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
        out string? error,
        string? framework = null)
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

        var useGenericProtocol = !string.IsNullOrWhiteSpace(framework);
        if (!TryNormalizeFramework(framework, out var frameworkId, out error))
            return false;

        if (!string.Equals(frameworkId, TestingFrameworkIds.NUnit, StringComparison.Ordinal)
            && HasNUnitSelection(names, tests, filterXml))
        {
            error = "--name, --test, and --filter are NUnit-only.";
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
            parentPid,
            frameworkId,
            useGenericProtocol);
        return true;
    }

    internal static bool TryNormalizeFramework(
        string? framework,
        out string frameworkId,
        out string? error)
    {
        frameworkId = TestingFrameworkIds.NUnit;
        error = null;

        if (string.IsNullOrWhiteSpace(framework))
            return true;

        var trimmed = framework.Trim();
        if (string.Equals(trimmed, TestingFrameworkIds.NUnit, StringComparison.OrdinalIgnoreCase))
        {
            frameworkId = TestingFrameworkIds.NUnit;
            return true;
        }

        error = $"Unsupported --framework '{trimmed}'.";
        return false;
    }

    private static bool HasNUnitSelection(
        IReadOnlyList<string>? names,
        IReadOnlyList<string>? tests,
        string? filterXml) =>
        (names?.Any(value => !string.IsNullOrWhiteSpace(value)) ?? false)
        || (tests?.Any(value => !string.IsNullOrWhiteSpace(value)) ?? false)
        || !string.IsNullOrWhiteSpace(filterXml);
}
