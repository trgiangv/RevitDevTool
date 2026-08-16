using System.Reflection;
using DevTools.Execution.Abstractions;
using DevTools.Hosting;

namespace DevTools.Utilities.AssemblyLoading;

/// <summary>
/// Identifies host API and framework assemblies that must be resolved from the
/// running host process, never from a plugin or test output directory.
/// Host-API names come from <see cref="DevTools.Hosting.IHostSharedAssemblyPolicy"/> after
/// <see cref="Use"/>; UI-package prefixes are owned by Execution.
/// </summary>
public static class HostSharedAssemblies
{
    private static readonly HashSet<string> FallbackHostApiNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "RevitAPI",
        "RevitAPIUI",
        "AdWindows",
        "acmgd",
        "acdbmgd",
        "accoremgd",
        "acdbmgdbrep"
    };

    private static readonly HashSet<string> DiscoveredNames = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] FallbackHostApiPrefixes =
    [
        "Autodesk.",
    ];

    private static readonly string[] FrameworkPrefixes =
    [
        "System.",
        "Microsoft.",
    ];

    private static readonly object InitLock = new();
    private static bool _configured;
    private static IHostSharedAssemblyPolicy? _policy;

    /// <summary>
    /// Sets the ambient host-API policy for static ALC hooks. Add-ins call this
    /// from <c>AddRevitInProcess</c> / <c>AddAutocadInProcess</c> before first load.
    /// </summary>
    public static void Use(IHostSharedAssemblyPolicy policy)
    {
        if (policy is null)
            throw new ArgumentNullException(nameof(policy));
        lock (InitLock)
        {
            _policy = policy;
        }
    }

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
        if (IsExplicitHostAssembly(assemblyName))
            return true;

        if (MatchesHostPackagePrefix(assemblyName))
            return true;

        foreach (var prefix in FrameworkPrefixes)
        {
            if (assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool MatchesHostPackagePrefix(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return false;

        foreach (var prefix in HostPackagePrefixes.Values)
        {
            if (assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var prefix in HostApiPrefixes)
        {
            if (assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether the name is an explicit host API/add-in assembly, without
    /// applying the broad System/Microsoft convenience prefixes used by command loading.
    /// </summary>
    public static bool IsExplicitHostAssembly(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return false;

        if (DiscoveredNames.Contains(assemblyName))
            return true;

        var policy = _policy;
        if (policy is not null)
            return ContainsIgnoreCase(policy.HostApiSimpleNames, assemblyName);

        return FallbackHostApiNames.Contains(assemblyName);
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

    private static IReadOnlyCollection<string> HostApiPrefixes
    {
        get
        {
            var policy = _policy;
            return policy is not null ? policy.HostApiPrefixes : FallbackHostApiPrefixes;
        }
    }

    private static bool ContainsIgnoreCase(IReadOnlyCollection<string> names, string assemblyName)
    {
        foreach (var name in names)
        {
            if (string.Equals(name, assemblyName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void PopulateSharedNames(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;

        foreach (var dll in Directory.GetFiles(directory, "*.dll"))
            DiscoveredNames.Add(Path.GetFileNameWithoutExtension(dll));
    }
}
