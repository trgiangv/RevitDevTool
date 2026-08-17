namespace DevTools.Utilities.AssemblyLoading;

/// <summary>
/// Identifies host API and framework assemblies that must be resolved from the
/// running host process, never from a plugin or test output directory.
/// Host-API names come from <see cref="HostSharedAssemblyNames"/> after
/// <see cref="Use"/>. There is no Revit+Acad fallback list — each add-in
/// calls <see cref="Use"/> with its own names.
/// </summary>
public static class HostSharedAssemblies
{
    private static readonly object InitLock = new();
    private static HostSharedAssemblyNames? _names;

    /// <summary>
    /// Sets the ambient host-API names for static ALC hooks. Add-ins call this
    /// at startup before first load, next to <c>AssemblyLoader.Initialize()</c>.
    /// </summary>
    public static void Use(HostSharedAssemblyNames names)
    {
        if (names is null)
            throw new ArgumentNullException(nameof(names));
        lock (InitLock)
        {
            _names = names;
        }
    }

    public static bool IsShared(string assemblyName)
    {
        if (IsExplicitHostAssembly(assemblyName))
            return true;

        if (MatchesHostPackagePrefix(assemblyName))
            return true;

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
    /// Returns whether the name is an explicitly declared host API assembly.
    /// </summary>
    public static bool IsExplicitHostAssembly(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return false;

        var names = _names;
        return names is not null && ContainsIgnoreCase(names.ExactNames, assemblyName);
    }

    private static IReadOnlyCollection<string> HostApiPrefixes
    {
        get
        {
            var names = _names;
            return names is not null ? names.Prefixes : [];
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
