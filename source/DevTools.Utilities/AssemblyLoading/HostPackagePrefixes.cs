namespace DevTools.Utilities.AssemblyLoading;

/// <summary>
/// UI-package prefixes that must resolve from the host add-in, never from a
/// plugin or test output directory. NUnit.Host calls through
/// <see cref="HostSharedAssemblies"/>.
/// </summary>
public static class HostPackagePrefixes
{
    public static readonly string[] Values =
    [
        "MahApps.",
        "ControlzEx.",
        "CommunityToolkit.",
    ];
}
