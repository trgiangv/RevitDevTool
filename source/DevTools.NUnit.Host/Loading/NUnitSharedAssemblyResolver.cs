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

        var fromDefault = FindLoaded(AssemblyLoadContext.Default, simpleName);
        if (fromDefault is not null)
            return fromDefault;

        // Plugin ALC (RevitDevTool bundle) often owns Roslyn/SRM copies that Default
        // has not materialized yet. Prefer those over a generation-private load.
        var pluginContext = AssemblyLoadContext.GetLoadContext(typeof(NUnitSharedAssemblyResolver).Assembly);
        if (pluginContext is not null && !ReferenceEquals(pluginContext, AssemblyLoadContext.Default))
            return FindLoaded(pluginContext, simpleName);

        // Returning null lets the CLR continue with Default for platform facades
        // that are not materialized in Default/Plugin yet.
        return null;
    }

    private static Assembly? FindLoaded(AssemblyLoadContext context, string simpleName)
    {
        foreach (var loaded in context.Assemblies)
        {
            if (string.Equals(simpleName, loaded.GetName().Name, StringComparison.OrdinalIgnoreCase))
                return loaded;
        }

        return null;
    }
}
#endif
