using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Host.Loading;

public sealed record TestingGenerationManifest(
    string GenerationId,
    TestFrameworkId FrameworkId,
    string SourceAssemblyPath,
    string ShadowDirectory,
    string ShadowAssemblyPath,
    string RuntimeAssemblyPath,
    IReadOnlyList<string> ManagedAssemblies,
    IReadOnlyList<string> NativeAssets,
    IReadOnlyList<string> SymbolFiles,
    IReadOnlyList<string> OtherFiles);
