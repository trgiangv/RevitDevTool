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

        if (requested.Version is not null && requested.Version != candidate.Version)
            return false;

        if (!string.Equals(NormalizeCulture(requested), NormalizeCulture(candidate), StringComparison.OrdinalIgnoreCase))
            return false;

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
