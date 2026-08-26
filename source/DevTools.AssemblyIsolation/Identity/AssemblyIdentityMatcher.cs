using System.Reflection;

namespace DevTools.AssemblyIsolation.Identity;

public static class AssemblyIdentityMatcher
{
    public static bool IsCompatible(AssemblyName requested, AssemblyName candidate)
    {
        if (requested is null) throw new ArgumentNullException(nameof(requested));
        if (candidate is null) throw new ArgumentNullException(nameof(candidate));

        if (!string.Equals(requested.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!VersionsCompatible(requested, candidate))
            return false;

        if (!string.Equals(NormalizeCulture(requested), NormalizeCulture(candidate), StringComparison.OrdinalIgnoreCase))
            return false;

        return TokensCompatible(requested, candidate);
    }

    /// <summary>
    /// Parent bindings from the host process: compile references carry NuGet or
    /// reference-assembly versions that do not match the host-loaded Autodesk API.
    /// Share by simple name, culture, and token only.
    /// </summary>
    public static bool IsCompatibleForParentShare(AssemblyName requested, AssemblyName parent)
    {
        if (requested is null) throw new ArgumentNullException(nameof(requested));
        if (parent is null) throw new ArgumentNullException(nameof(parent));

        if (!string.Equals(requested.Name, parent.Name, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(NormalizeCulture(requested), NormalizeCulture(parent), StringComparison.OrdinalIgnoreCase))
            return false;

        return TokensCompatible(requested, parent);
    }

    static bool VersionsCompatible(AssemblyName requested, AssemblyName candidate)
        => requested.Version is null || requested.Version == candidate.Version;

    static bool TokensCompatible(AssemblyName requested, AssemblyName candidate)
    {
        var requestedToken = requested.GetPublicKeyToken();
        return requestedToken is null
               || requestedToken.Length == 0
               || requestedToken.SequenceEqual(candidate.GetPublicKeyToken() ?? []);
    }

    static string NormalizeCulture(AssemblyName identity)
    {
        var culture = identity.CultureName;
        return string.IsNullOrWhiteSpace(culture) || string.Equals(culture, "neutral", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : culture;
    }
}
