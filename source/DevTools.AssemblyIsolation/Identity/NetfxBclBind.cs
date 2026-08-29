#if NETFRAMEWORK
using System.Reflection;

namespace DevTools.AssemblyIsolation.Identity;

/// <summary>
/// net48 only. TUnit.Engine netstandard2.0 requests STJ 9; payload ships CPM 10.
/// Testhost uses binding redirects; isolated AppDomain resolve does not.
/// </summary>
internal static class NetfxBclBind
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.Text.Json",
        "Microsoft.Bcl.AsyncInterfaces",
        "System.IO.Pipelines",
        "System.Text.Encodings.Web",
    };

    internal static bool AllowsNewer(AssemblyName requested, AssemblyName candidate)
    {
        if (requested is null) throw new ArgumentNullException(nameof(requested));
        if (candidate is null) throw new ArgumentNullException(nameof(candidate));

        if (requested.Name is null || !Names.Contains(requested.Name))
            return false;

        if (!AssemblyIdentityMatcher.IsCompatible(requested, candidate, allowVersionDrift: true))
            return false;

        if (requested.Version is null)
            return true;

        return candidate.Version is not null && candidate.Version >= requested.Version;
    }
}
#endif
