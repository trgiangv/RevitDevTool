#if NET
using System.Reflection;
using System.Runtime.Loader;

namespace DevTools.NUnit.Host.Loading;

internal static class NUnitSharedAssemblyResolver
{
    internal static Assembly? TryResolveFromDefault(AssemblyName requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        var simpleName = requested.Name
            ?? throw new NUnitGenerationAssemblyResolutionException("Requested assembly name is missing.");

        if (!NUnitSharedAssemblyPolicy.IsShared(simpleName))
        {
            throw new NUnitGenerationAssemblyResolutionException(
                $"Assembly '{simpleName}' is not allowlisted for default-context sharing.");
        }

        foreach (var loaded in AssemblyLoadContext.Default.Assemblies)
        {
            if (string.Equals(
                    simpleName,
                    loaded.GetName().Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return loaded;
            }
        }

        // Shared policy never loads another copy from disk. Returning null lets
        // the CLR handle platform assemblies that are not materialized in
        // Default.Assemblies yet.
        return null;
    }

}
#endif
