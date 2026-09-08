#if NETFRAMEWORK
using System.Reflection;

namespace DevTools.AssemblyIsolation.Identity;

/// <summary>
/// net48 only. Assembly identity is type identity; testhost unifies a process
/// with binding redirects. Revit DefaultDomain cannot — other add-ins already
/// loaded other versions. An isolated <see cref="Sources.ManifestAssemblySource"/>
/// is the redirect closure for one session: a request may bind a newer candidate
/// already in that manifest or already loaded by this session (name, culture,
/// token; never a downgrade; never a DefaultDomain copy this session did not
/// load). Directory sources and CoreCLR stay exact. Not a TUnit name list.
/// </summary>
internal static class NetfxClosureBind
{
    internal static bool AllowsNewer(AssemblyName requested, AssemblyName candidate)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(candidate);

        if (!AssemblyIdentityMatcher.IsCompatible(requested, candidate, allowVersionDrift: true))
            return false;

        if (requested.Version is null)
            return true;

        return candidate.Version is not null && candidate.Version >= requested.Version;
    }

    /// <summary>
    /// Reuse a copy this session already loaded. Do not scan DefaultDomain.
    /// </summary>
    internal static bool TryFindLoaded(
        AssemblyName requested,
        IEnumerable<Assembly> assemblies,
        out Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(assemblies);

        Assembly? newest = null;
        foreach (var candidate in assemblies)
        {
            if (candidate is null || candidate.IsDynamic)
                continue;

            var identity = candidate.GetName();
            if (!AssemblyIdentityMatcher.IsCompatible(requested, identity)
                && !AllowsNewer(requested, identity))
                continue;

            if (newest is null
                || identity.Version is not null
                && (newest.GetName().Version is null || identity.Version > newest.GetName().Version))
            {
                newest = candidate;
            }
        }

        assembly = newest!;
        return newest is not null;
    }
}
#endif
