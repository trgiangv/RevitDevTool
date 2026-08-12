namespace DevTools.NUnit.Host.Loading;

public sealed record NUnitGenerationManifest(
    string GenerationId,
    string SourceAssemblyPath,
    string ShadowDirectory,
    string ShadowAssemblyPath,
    string RuntimeAssemblyPath,
    string FrameworkAssemblyPath,
    IReadOnlyList<string> ManagedAssemblies,
    IReadOnlyList<string> NativeAssets,
    string? SymbolPath);

public interface INUnitGenerationBuilder
{
    NUnitGenerationManifest Build(string testAssemblyPath);
}
