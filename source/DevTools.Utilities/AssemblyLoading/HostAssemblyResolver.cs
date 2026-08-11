using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif

namespace DevTools.Utilities.AssemblyLoading;

/// <summary>
/// Reuses assemblies already loaded in the current <see cref="AppDomain"/>.
/// First step for any host-aware resolve — never reload host or third-party DLLs.
/// </summary>
public static class HostAssemblyResolver
{
    private static int _registered;

    /// <summary>
    /// Registers process-wide resolve hooks that reuse already-loaded <em>host/shared</em> assemblies.
    /// Does not bind arbitrary names (for example <c>nunit.framework</c>).
    /// Safe to call multiple times.
    /// </summary>
    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0)
            return;

        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
#if NET
        AssemblyLoadContext.Default.Resolving += (_, name) => HostSharedAssemblies.TryResolveFromHost(name);
#endif
    }

    /// <summary>
    /// Returns an already-loaded assembly with the same simple name.
    /// </summary>
    public static Assembly? ResolveFromAppDomain(AssemblyName assemblyName)
    {
        var simpleName = assemblyName.Name;
        if (string.IsNullOrWhiteSpace(simpleName))
            return null;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var loaded = assembly.GetName();
            if (string.Equals(loaded.Name, simpleName, StringComparison.OrdinalIgnoreCase))
                return assembly;
        }

        return null;
    }

    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args) =>
        args.Name is null ? null : HostSharedAssemblies.TryResolveFromHost(new AssemblyName(args.Name));
}
