using System.Reflection;

namespace DevTools.Utilities.AssemblyLoading;

/// <summary>
/// Identifies host API and framework assemblies that must be resolved from the
/// running host process, never from a plugin or test output directory.
/// </summary>
public static class HostSharedAssemblies
{
    private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "RevitAPI",
        "RevitAPIUI",
        "AdWindows",
        "acmgd",
        "acdbmgd",
        "accoremgd",
        "acdbmgdbrep"
    };

    private static readonly string[] SharedPrefixes =
    [
        "System.", "Microsoft.", "MahApps.", "ControlzEx.",
        "CommunityToolkit.", "Autodesk."
    ];

    private static readonly object InitLock = new();
    private static bool _configured;

    /// <summary>
    /// Registers additional shared assembly simple names discovered in host directories.
    /// Safe to call multiple times; only the first call applies.
    /// </summary>
    public static void Configure(string hostApiDirectory, string hostAddinDirectory)
    {
        lock (InitLock)
        {
            if (_configured)
                return;

            PopulateSharedNames(hostApiDirectory);
            if (!string.Equals(hostApiDirectory, hostAddinDirectory, StringComparison.OrdinalIgnoreCase))
                PopulateSharedNames(hostAddinDirectory);

            _configured = true;
        }
    }

    public static bool IsShared(string assemblyName)
    {
        if (SharedAssemblyNames.Contains(assemblyName))
            return true;

        foreach (var prefix in SharedPrefixes)
        {
            if (assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns a host/shared assembly already loaded in the current AppDomain.
    /// Never loads from disk.
    /// </summary>
    public static Assembly? TryResolveFromHost(AssemblyName assemblyName)
    {
        var simpleName = assemblyName.Name;
        if (string.IsNullOrWhiteSpace(simpleName) || !IsShared(simpleName))
            return null;

        return HostAssemblyResolver.ResolveFromAppDomain(assemblyName);
    }

    private static void PopulateSharedNames(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;

        foreach (var dll in Directory.GetFiles(directory, "*.dll"))
            SharedAssemblyNames.Add(Path.GetFileNameWithoutExtension(dll));
    }
}
