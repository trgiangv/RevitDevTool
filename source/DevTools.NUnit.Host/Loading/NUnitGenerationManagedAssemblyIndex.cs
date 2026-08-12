#if NET
using System.Collections.Frozen;
using System.Reflection;

namespace DevTools.NUnit.Host.Loading;

internal readonly record struct ManagedAssemblyEntry(AssemblyName Identity, string Path);

internal sealed class NUnitGenerationManagedAssemblyIndex
{
    private readonly FrozenDictionary<string, IReadOnlyList<ManagedAssemblyEntry>> _entriesBySimpleName;

    private NUnitGenerationManagedAssemblyIndex(
        FrozenDictionary<string, IReadOnlyList<ManagedAssemblyEntry>> entriesBySimpleName)
    {
        _entriesBySimpleName = entriesBySimpleName;
    }

    internal static NUnitGenerationManagedAssemblyIndex Create(IReadOnlyList<string> managedAssemblies)
    {
        var groups = new Dictionary<string, List<ManagedAssemblyEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var absolutePath in managedAssemblies)
        {
            AssemblyName identity;
            try
            {
                identity = AssemblyName.GetAssemblyName(absolutePath);
            }
            catch (Exception ex)
            {
                throw new NUnitGenerationLoadException(
                    $"Managed generation asset is not a valid assembly: {absolutePath}",
                    ex);
            }

            var simpleName = identity.Name
                ?? throw new NUnitGenerationLoadException(
                    $"Managed generation asset has no assembly name: {absolutePath}");

            if (!groups.TryGetValue(simpleName, out var entries))
            {
                entries = [];
                groups[simpleName] = entries;
            }

            entries.Add(new ManagedAssemblyEntry(identity, Path.GetFullPath(absolutePath)));
        }

        foreach (var (simpleName, entries) in groups)
        {
            if (entries.Count <= 1)
                continue;

            if (HasAmbiguousIdentity(entries))
            {
                throw new NUnitGenerationLoadException(
                    $"Ambiguous managed assembly identities for '{simpleName}' in generation manifest.");
            }
        }

        var frozenGroups = groups.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<ManagedAssemblyEntry>)pair.Value.ToList(),
            StringComparer.OrdinalIgnoreCase);

        return new NUnitGenerationManagedAssemblyIndex(frozenGroups.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
    }

    internal string? ResolvePath(AssemblyName requested)
    {
        var simpleName = requested.Name;
        if (string.IsNullOrWhiteSpace(simpleName))
            return null;

        if (!_entriesBySimpleName.TryGetValue(simpleName, out var entries) || entries.Count == 0)
            return null;

        var compatible = entries
            .Where(entry => IsCompatibleIdentity(requested, entry.Identity))
            .ToList();

        if (compatible.Count > 1)
        {
            throw new NUnitGenerationAssemblyResolutionException(
                $"Ambiguous managed assembly matches for '{requested.FullName}' in generation manifest.");
        }

        if (compatible.Count == 1)
            return compatible[0].Path;

        return null;
    }

    internal static bool IsCompatibleIdentity(AssemblyName requested, AssemblyName candidate)
    {
        if (!string.Equals(requested.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))
            return false;

        if (requested.Version is not null && !VersionEquals(requested.Version, candidate.Version))
            return false;

        // Satellite assemblies share a simple name (*.resources) but differ by culture.
        // Always compare normalized cultures so neutral requests do not match every satellite.
        if (!string.Equals(
                NormalizeCulture(requested.CultureName),
                NormalizeCulture(candidate.CultureName),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requestedToken = requested.GetPublicKeyToken();
        if (requestedToken is { Length: > 0 })
        {
            var candidateToken = candidate.GetPublicKeyToken();
            if (candidateToken is not { Length: > 0 }
                || !requestedToken.AsSpan().SequenceEqual(candidateToken))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasAmbiguousIdentity(IReadOnlyList<ManagedAssemblyEntry> entries)
    {
        var byCulture = new Dictionary<string, List<ManagedAssemblyEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var culture = NormalizeCulture(entry.Identity.CultureName);
            if (!byCulture.TryGetValue(culture, out var cultureEntries))
            {
                cultureEntries = [];
                byCulture[culture] = cultureEntries;
            }

            cultureEntries.Add(entry);
        }

        foreach (var cultureEntries in byCulture.Values)
        {
            for (var i = 0; i < cultureEntries.Count; i++)
            {
                for (var j = i + 1; j < cultureEntries.Count; j++)
                {
                    if (!IdentityEquivalent(cultureEntries[i].Identity, cultureEntries[j].Identity))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool IdentityEquivalent(AssemblyName left, AssemblyName right) =>
        string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
        && VersionEquals(left.Version, right.Version)
        && string.Equals(
            NormalizeCulture(left.CultureName),
            NormalizeCulture(right.CultureName),
            StringComparison.OrdinalIgnoreCase)
        && TokenEquals(left.GetPublicKeyToken(), right.GetPublicKeyToken());

    internal static string NormalizeCulture(string? cultureName) =>
        string.IsNullOrEmpty(cultureName)
        || string.Equals(cultureName, "neutral", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : cultureName!;

    private static bool VersionEquals(Version? left, Version? right)
    {
        if (left is null || right is null)
            return true;

        return left.Major == right.Major
            && left.Minor == right.Minor
            && left.Build == right.Build
            && left.Revision == right.Revision;
    }

    private static bool TokenEquals(byte[]? left, byte[]? right)
    {
        var leftSpecified = left is { Length: > 0 };
        var rightSpecified = right is { Length: > 0 };

        if (leftSpecified && !rightSpecified)
            return false;

        if (!leftSpecified)
            return true;

        return left!.AsSpan().SequenceEqual(right!);
    }
}
#endif
