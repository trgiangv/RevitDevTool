namespace DevTools.Execution.Abstractions;

// Execution owns these UI-package prefixes; NUnit.Host calls through HostSharedAssemblies.
public static class HostPackagePrefixes
{
    public static readonly string[] Values =
    [
        "MahApps.",
        "ControlzEx.",
        "CommunityToolkit.",
    ];
}
