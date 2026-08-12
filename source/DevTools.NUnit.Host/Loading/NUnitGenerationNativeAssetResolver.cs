#if NET
using System.Runtime.Loader;

namespace DevTools.NUnit.Host.Loading;

internal static class NUnitGenerationNativeAssetResolver
{
    internal static string? Resolve(
        string unmanagedDllName,
        AssemblyDependencyResolver resolver,
        IReadOnlySet<string> manifestNativeAssetPaths,
        string shadowDirectory,
        IReadOnlyDictionary<string, IReadOnlyList<string>> nativeAssetsByFileName)
    {
        if (string.IsNullOrWhiteSpace(unmanagedDllName))
            return null;

        var resolverPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (resolverPath is not null
            && TryAcceptManifestNativePath(resolverPath, manifestNativeAssetPaths, shadowDirectory, out var acceptedResolverPath))
        {
            return acceptedResolverPath;
        }

        return ResolveUniqueManifestFallback(
            unmanagedDllName,
            manifestNativeAssetPaths,
            shadowDirectory,
            nativeAssetsByFileName);
    }

    private static string? ResolveUniqueManifestFallback(
        string unmanagedDllName,
        IReadOnlySet<string> manifestNativeAssetPaths,
        string shadowDirectory,
        IReadOnlyDictionary<string, IReadOnlyList<string>> nativeAssetsByFileName)
    {
        var lookupKeys = BuildLookupKeys(unmanagedDllName);
        IReadOnlyList<string>? matchedPaths = null;

        foreach (var lookupKey in lookupKeys)
        {
            if (!nativeAssetsByFileName.TryGetValue(lookupKey, out var candidates) || candidates.Count == 0)
                continue;

            if (matchedPaths is not null)
            {
                if (!PathsEquivalent(matchedPaths, candidates))
                {
                    throw new NUnitGenerationLoadException(
                        $"Ambiguous native asset lookup for '{unmanagedDllName}'. Multiple manifest filename groups match.");
                }

                continue;
            }

            matchedPaths = candidates;
        }

        if (matchedPaths is null || matchedPaths.Count == 0)
            return null;

        if (matchedPaths.Count > 1)
        {
            throw new NUnitGenerationLoadException(
                $"Ambiguous native asset '{unmanagedDllName}' maps to {matchedPaths.Count} manifest paths: "
                + string.Join(", ", matchedPaths));
        }

        var candidate = matchedPaths[0];
        if (!TryAcceptManifestNativePath(candidate, manifestNativeAssetPaths, shadowDirectory, out var acceptedPath))
            return null;

        return acceptedPath;
    }

    private static bool TryAcceptManifestNativePath(
        string candidatePath,
        IReadOnlySet<string> manifestNativeAssetPaths,
        string shadowDirectory,
        out string acceptedPath)
    {
        acceptedPath = Path.GetFullPath(candidatePath);
        if (!manifestNativeAssetPaths.Contains(acceptedPath))
            return false;

        if (!IsUnderShadowDirectory(acceptedPath, shadowDirectory))
            return false;

        return File.Exists(acceptedPath);
    }

    private static IEnumerable<string> BuildLookupKeys(string unmanagedDllName)
    {
        yield return unmanagedDllName;

        var fileName = unmanagedDllName;
        if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            fileName += ".dll";

        if (!string.Equals(fileName, unmanagedDllName, StringComparison.OrdinalIgnoreCase))
            yield return fileName;

        var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        if (!string.IsNullOrWhiteSpace(withoutExtension)
            && !string.Equals(withoutExtension, fileName, StringComparison.OrdinalIgnoreCase))
        {
            yield return withoutExtension;
        }
    }

    private static bool IsUnderShadowDirectory(string absolutePath, string shadowDirectory)
    {
        var normalizedPath = Path.GetFullPath(absolutePath);
        var normalizedShadow = Path.GetFullPath(shadowDirectory);

        if (string.Equals(normalizedPath, normalizedShadow, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = normalizedShadow.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedShadow
            : normalizedShadow + Path.DirectorySeparatorChar;

        return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEquivalent(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
            return false;

        return left
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(right.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
    }
}
#endif
