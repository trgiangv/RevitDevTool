namespace DevTools.NUnit.Provider;

/// <summary>
/// Host contract passed to <c>DevTools.TestRunner</c>. Shared by MTP and VSTest.
/// Used through project references by those assemblies; not compiled into the in-host runtime DLL.
/// </summary>
public sealed record HostRunOptions(
    string Host,
    string HostVersion,
    bool HostLaunch,
    int HostTimeoutSeconds,
    int HostLaunchTimeoutSeconds,
    string? RunnerPath,
    int? DebugParentPid = null);
