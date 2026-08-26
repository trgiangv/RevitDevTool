using System.Reflection;

namespace DevTools.AssemblyIsolation.Identity;

public static class AssemblyIdentityMatcher
{
    public static bool IsCompatible(
        AssemblyName requested,
        AssemblyName candidate,
        bool allowVersionDrift = false)
    {
        if (requested is null) throw new ArgumentNullException(nameof(requested));
        if (candidate is null) throw new ArgumentNullException(nameof(candidate));

        if (!string.Equals(requested.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!allowVersionDrift && !VersionsCompatible(requested, candidate))
            return false;

        if (!string.Equals(NormalizeCulture(requested), NormalizeCulture(candidate), StringComparison.OrdinalIgnoreCase))
            return false;

        return TokensCompatible(requested, candidate);
    }

    private static bool VersionsCompatible(AssemblyName requested, AssemblyName candidate)
        => requested.Version is null || requested.Version == candidate.Version;

    private static bool TokensCompatible(AssemblyName requested, AssemblyName candidate)
    {
        var requestedToken = requested.GetPublicKeyToken();
        return requestedToken is null
               || requestedToken.Length == 0
               || requestedToken.SequenceEqual(candidate.GetPublicKeyToken() ?? []);
    }

    private static string NormalizeCulture(AssemblyName identity)
    {
        var culture = identity.CultureName;
        return string.IsNullOrWhiteSpace(culture) || string.Equals(culture, "neutral", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : culture;
    }
}
