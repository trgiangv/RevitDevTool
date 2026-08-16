using System.Reflection;

namespace DevTools.Utilities.AssemblyLoading;

/// <summary>
/// Looks up assemblies already loaded in the current <see cref="AppDomain"/>.
/// Command ALC returns null for shared names so the default context supplies
/// the host copy — this type does not register process-wide resolve hooks.
/// </summary>
public static class HostAssemblyResolver
{
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
}
