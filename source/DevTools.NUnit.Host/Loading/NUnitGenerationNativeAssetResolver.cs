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
        if (!TryFindUniqueFilenameGroup(unmanagedDllName, nativeAssetsByFileName, out var matchedPaths))
            return null;

        var candidate = RequireSingleManifestPath(unmanagedDllName, matchedPaths);
        return TryAcceptManifestNativePath(candidate, manifestNativeAssetPaths, shadowDirectory, out var acceptedPath)
            ? acceptedPath
            : null;
    }

    private static bool TryFindUniqueFilenameGroup(
        string unmanagedDllName,
        IReadOnlyDictionary<string, IReadOnlyList<string>> nativeAssetsByFileName,
        out IReadOnlyList<string> matchedPaths)
    {
        IReadOnlyList<string>? matched = null;
        foreach (var lookupKey in BuildLookupKeys(unmanagedDllName))
        {
            if (!TryGetFilenameCandidates(lookupKey, nativeAssetsByFileName, out var candidates))
                continue;

            MergeFilenameGroup(unmanagedDllName, ref matched, candidates);
        }

        if (matched is null || matched.Count == 0)
        {
            matchedPaths = Array.Empty<string>();
            return false;
        }

        matchedPaths = matched;
        return true;
    }

    private static bool TryGetFilenameCandidates(
        string lookupKey,
        IReadOnlyDictionary<string, IReadOnlyList<string>> nativeAssetsByFileName,
        out IReadOnlyList<string> candidates)
    {
        if (nativeAssetsByFileName.TryGetValue(lookupKey, out var found) && found.Count > 0)
        {
            candidates = found;
            return true;
        }

        candidates = Array.Empty<string>();
        return false;
    }

    private static void MergeFilenameGroup(
        string unmanagedDllName,
        ref IReadOnlyList<string>? matchedPaths,
        IReadOnlyList<string> candidates)
    {
        if (matchedPaths is null)
        {
            matchedPaths = candidates;
            return;
        }

        if (PathsEquivalent(matchedPaths, candidates))
            return;

        throw new NUnitGenerationLoadException(
            $"Ambiguous native asset lookup for '{unmanagedDllName}'. Multiple manifest filename groups match.");
    }

    private static string RequireSingleManifestPath(string unmanagedDllName, IReadOnlyList<string> matchedPaths)
    {
        if (matchedPaths.Count == 1)
            return matchedPaths[0];

        throw new NUnitGenerationLoadException(
            $"Ambiguous native asset '{unmanagedDllName}' maps to {matchedPaths.Count} manifest paths: "
            + string.Join(", ", matchedPaths));
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
