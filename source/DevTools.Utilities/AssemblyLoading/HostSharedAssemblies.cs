namespace DevTools.Utilities.AssemblyLoading;

/// <summary>
/// Identifies host API and framework assemblies that must be resolved from the
/// running host process, never from a plugin or test output directory.
/// Host-API names come from <see cref="HostApiAssemblySet"/> after
/// <see cref="Use"/>. There is no Revit+Acad fallback list — each add-in
/// calls <see cref="Use"/> with its own set.
/// </summary>
public static class HostSharedAssemblies
{
    private static readonly string[] FrameworkPrefixes =
    [
        "System.",
        "Microsoft.",
    ];

    private static readonly object InitLock = new();
    private static HostApiAssemblySet? _set;

    /// <summary>
    /// Sets the ambient host-API names for static ALC hooks. Add-ins call this
    /// at startup before first load, next to <c>AssemblyLoader.Initialize()</c>.
    /// </summary>
    public static void Use(HostApiAssemblySet set)
    {
        if (set is null)
            throw new ArgumentNullException(nameof(set));
        lock (InitLock)
        {
            _set = set;
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
    /// Returns whether the name is an explicit host API assembly, without
    /// applying the broad System/Microsoft convenience prefixes used by command loading.
    /// </summary>
    public static bool IsExplicitHostAssembly(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return false;

        var set = _set;
        return set is not null && ContainsIgnoreCase(set.SimpleNames, assemblyName);
    }

    private static IReadOnlyCollection<string> HostApiPrefixes
    {
        get
        {
            var set = _set;
            return set is not null ? set.Prefixes : [];
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
}
