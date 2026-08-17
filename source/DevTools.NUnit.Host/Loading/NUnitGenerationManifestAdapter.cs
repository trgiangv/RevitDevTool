using DevTools.Testing.Host.Loading;

namespace DevTools.NUnit.Host.Loading;

internal static class NUnitGenerationManifestAdapter
{
    internal static NUnitGenerationManifest ToNUnit(TestingGenerationManifest generation)
    {
        var frameworkPath = generation.ManagedAssemblies.SingleOrDefault(path =>
            string.Equals(Path.GetFileName(path), NUnitGenerationPolicy.FrameworkAssemblyFileName, StringComparison.OrdinalIgnoreCase));
        if (frameworkPath is null)
        {
            throw new NUnitGenerationBuildException(
                $"Published generation {generation.GenerationId} is missing {NUnitGenerationPolicy.FrameworkAssemblyFileName}.");
        }

        var symbolPath = generation.SymbolFiles.SingleOrDefault(path => string.Equals(
            Path.GetFileNameWithoutExtension(path),
            Path.GetFileNameWithoutExtension(generation.ShadowAssemblyPath),
            StringComparison.OrdinalIgnoreCase));

        return new NUnitGenerationManifest(
            generation.GenerationId,
            generation.SourceAssemblyPath,
            generation.ShadowDirectory,
            generation.ShadowAssemblyPath,
            generation.RuntimeAssemblyPath,
            frameworkPath,
            generation.ManagedAssemblies,
            generation.NativeAssets,
            symbolPath);
    }
}
