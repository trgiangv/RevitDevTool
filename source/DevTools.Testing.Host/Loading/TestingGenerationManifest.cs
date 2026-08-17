namespace DevTools.Testing.Host.Loading;

public sealed record TestingGenerationManifest(
    string GenerationId,
    string FrameworkId,
    string SourceAssemblyPath,
    string ShadowDirectory,
    string ShadowAssemblyPath,
    string RuntimeAssemblyPath,
    string FrameworkAssemblyPath,
    IReadOnlyList<string> ManagedAssemblies,
    IReadOnlyList<string> NativeAssets);
