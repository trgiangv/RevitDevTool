namespace DevTools.NUnit.Core;

/// <summary>
/// Host contract passed to <c>DevTools.NUnit.Runner</c>. Shared by MTP and VSTest.
/// Linked into those assemblies; not compiled into the in-host Core DLL.
/// </summary>
public sealed record HostRunOptions(
    string Host,
    string HostVersion,
    bool HostLaunch,
    int HostTimeoutSeconds,
    int HostLaunchTimeoutSeconds,
    string? RunnerPath,
    int? DebugParentPid = null);
