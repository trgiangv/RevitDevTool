namespace DevTools.NUnit.Host.Loading;

internal static class NUnitGenerationManifestFactory
{
    internal static NUnitGenerationManifest FromPublishedSnapshot(
        string generationId,
        string sourceAssemblyPath,
        string shadowDirectory)
    {
        var sourceOutputDirectory = Path.GetDirectoryName(sourceAssemblyPath)
            ?? throw new NUnitGenerationBuildException(
                $"Test assembly path has no directory: {sourceAssemblyPath}");

        var sourceAssemblyRelativePath = NUnitGenerationPaths.NormalizeRelativePath(
            NUnitGenerationPaths.GetRelativePath(sourceOutputDirectory, sourceAssemblyPath));

        var classified = ClassifyPublishedFiles(shadowDirectory);
        if (classified.FrameworkRelativePath is null)
        {
            throw new NUnitGenerationBuildException(
                $"Published generation {generationId} is missing {NUnitGenerationBuilder.FrameworkAssemblyFileName}.");
        }

        var testSymbolCandidate = Path.ChangeExtension(sourceAssemblyRelativePath, ".pdb");
        string? testSymbolRelativePath = File.Exists(Path.Combine(shadowDirectory, testSymbolCandidate))
            ? testSymbolCandidate
            : null;

        classified.ManagedAssemblies.Sort(StringComparer.OrdinalIgnoreCase);
        classified.NativeAssets.Sort(StringComparer.OrdinalIgnoreCase);

        return CreateManifest(
            generationId,
            sourceAssemblyPath,
            shadowDirectory,
            sourceAssemblyRelativePath,
            testSymbolRelativePath,
            classified.FrameworkRelativePath,
            classified.ManagedAssemblies,
            classified.NativeAssets);
    }

    private static PublishedSnapshotFiles ClassifyPublishedFiles(string shadowDirectory)
    {
        var managedAssemblies = new List<string>();
        var nativeAssets = new List<string>();
        string? frameworkRelativePath = null;

        foreach (var absolutePath in Directory.EnumerateFiles(shadowDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = NUnitGenerationPaths.NormalizeRelativePath(
                NUnitGenerationPaths.GetRelativePath(shadowDirectory, absolutePath));
            if (string.Equals(
                    relativePath,
                    NUnitGenerationBuilder.GenerationCompleteMarkerFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (NUnitSharedAssemblyPolicy.TryGetManagedAssemblyIdentity(absolutePath, out var simpleName))
            {
                managedAssemblies.Add(relativePath);
                if (IsFrameworkSimpleName(simpleName))
                    frameworkRelativePath = relativePath;
            }
            else if (IsNativeAsset(absolutePath))
            {
                nativeAssets.Add(relativePath);
            }
        }

        return new PublishedSnapshotFiles(managedAssemblies, nativeAssets, frameworkRelativePath);
    }

    private static bool IsFrameworkSimpleName(string? simpleName) =>
        string.Equals(
            simpleName,
            Path.GetFileNameWithoutExtension(NUnitGenerationBuilder.FrameworkAssemblyFileName),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsNativeAsset(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".dll", StringComparison.OrdinalIgnoreCase)
        && !NUnitSharedAssemblyPolicy.TryGetManagedAssemblyIdentity(filePath, out _);

    private static NUnitGenerationManifest CreateManifest(
        string generationId,
        string sourceAssemblyPath,
        string shadowDirectory,
        string sourceAssemblyRelativePath,
        string? testSymbolRelativePath,
        string frameworkRelativePath,
        IReadOnlyList<string> managedAssemblies,
        IReadOnlyList<string> nativeAssets)
    {
        var shadowAssemblyPath = Path.Combine(shadowDirectory, sourceAssemblyRelativePath);
        var runtimeAssemblyPath = Path.Combine(shadowDirectory, NUnitGenerationBuilder.RuntimeAssemblyFileName);
        var frameworkAssemblyPath = Path.Combine(shadowDirectory, frameworkRelativePath);
        string? symbolPath = null;

        if (!string.IsNullOrWhiteSpace(testSymbolRelativePath))
        {
            var candidate = Path.Combine(shadowDirectory, testSymbolRelativePath);
            if (File.Exists(candidate))
                symbolPath = candidate;
        }

        return new NUnitGenerationManifest(
            generationId,
            sourceAssemblyPath,
            shadowDirectory,
            shadowAssemblyPath,
            runtimeAssemblyPath,
            frameworkAssemblyPath,
            managedAssemblies
                .Select(relative => Path.Combine(shadowDirectory, relative))
                .ToList(),
            nativeAssets
                .Select(relative => Path.Combine(shadowDirectory, relative))
                .ToList(),
            symbolPath);
    }

    private sealed record PublishedSnapshotFiles(
        List<string> ManagedAssemblies,
        List<string> NativeAssets,
        string? FrameworkRelativePath);
}
