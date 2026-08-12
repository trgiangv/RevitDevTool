#if NETFRAMEWORK
using System.Reflection;

namespace DevTools.NUnit.Host.Loading;

/// <summary>
/// Reuses host/shared assemblies already loaded in the current AppDomain for net48.
/// </summary>
internal static class NetFrameworkNUnitSharedAssemblyResolver
{
    internal static Assembly? TryResolveFromAppDomain(AssemblyName requested)
    {
        if (requested is null)
            throw new ArgumentNullException(nameof(requested));

        var simpleName = requested.Name;
        if (string.IsNullOrWhiteSpace(simpleName) || !NUnitSharedAssemblyPolicy.IsShared(simpleName))
            return null;

        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(loaded.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                return loaded;
        }

        return null;
    }
}
#endif
