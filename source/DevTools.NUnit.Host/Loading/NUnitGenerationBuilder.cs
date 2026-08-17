using DevTools.Testing.Host.Loading;

namespace DevTools.NUnit.Host.Loading;

public class NUnitGenerationBuildException : Exception
{
    public NUnitGenerationBuildException(string message) : base(message) { }
    public NUnitGenerationBuildException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class NUnitGenerationCorruptionException : NUnitGenerationBuildException
{
    public NUnitGenerationCorruptionException(string shadowDirectory, string expectedGenerationId, string actualGenerationId)
        : base($"Published generation at '{shadowDirectory}' is corrupted: expected generation ID '{expectedGenerationId}', actual content hash '{actualGenerationId}'.")
    {
        ShadowDirectory = shadowDirectory;
        ExpectedGenerationId = expectedGenerationId;
        ActualGenerationId = actualGenerationId;
    }

    public string ShadowDirectory { get; }
    public string ExpectedGenerationId { get; }
    public string ActualGenerationId { get; }
}

public sealed record NUnitRuntimeSource(string AssemblyPath, string? SymbolPath, IReadOnlyList<string> DependencyPaths);
public delegate NUnitRuntimeSource NUnitRuntimeSourcePathProvider();

internal enum SnapshotCopyPhase
{
    BeforeCopy,
    AfterCopy,
}

/// <summary>
/// Compatibility facade for legacy NUnit callers. Copy/hash/publish mechanics
/// live in <see cref="TestingGenerationStore"/>; this facade owns no snapshot state.
/// </summary>
public sealed class NUnitGenerationBuilder : INUnitGenerationBuilder
{
    public const string RuntimeAssemblyFileName = NUnitGenerationPolicy.RuntimeAssemblyFileName;
    public const string RuntimeSymbolFileName = NUnitGenerationPolicy.RuntimeSymbolFileName;
    public const string FrameworkAssemblyFileName = NUnitGenerationPolicy.FrameworkAssemblyFileName;
    public const string GenerationCompleteMarkerFileName = ".generation-complete";
    internal const string ExpectedNUnitFileVersion = NUnitGenerationPolicy.ExpectedNUnitFileVersion;
    internal const string ExpectedNUnitPackageVersion = NUnitGenerationPolicy.ExpectedNUnitPackageVersion;

    private readonly TestingGenerationStore _store;
    private readonly NUnitGenerationPolicy _policy;

    internal TestingGenerationStore Store => _store;
    internal NUnitGenerationPolicy Policy => _policy;
    internal Action? AfterSnapshotBeforePublishHook { get; set; }
    internal Action<string, SnapshotCopyPhase>? SnapshotCopyProgressHook { get; set; }

    public NUnitGenerationBuilder(NUnitRuntimeSourcePathProvider runtimeSourcePathProvider, string? generationsRootDirectory = null)
    {
        _store = new TestingGenerationStore(generationsRootDirectory
            ?? Path.Combine(Path.GetTempPath(), "DevTools", "NUnit", "Generations"));
        _policy = new NUnitGenerationPolicy(runtimeSourcePathProvider);
    }

    public NUnitGenerationManifest Build(string testAssemblyPath)
    {
        try
        {
            _store.BeforeFileCopied = path => SnapshotCopyProgressHook?.Invoke(path, SnapshotCopyPhase.BeforeCopy);
            _store.AfterFileCopied = path => SnapshotCopyProgressHook?.Invoke(path, SnapshotCopyPhase.AfterCopy);
            _store.BeforePublish = () => AfterSnapshotBeforePublishHook?.Invoke();
            return NUnitGenerationManifestAdapter.ToNUnit(_store.Build(_policy, testAssemblyPath));
        }
        catch (NUnitGenerationBuildException)
        {
            throw;
        }
        catch (TestingGenerationCorruptionException ex)
        {
            throw new NUnitGenerationCorruptionException(ex.ShadowDirectory, ex.ExpectedGenerationId, ex.ActualGenerationId);
        }
        catch (TestingGenerationBuildException ex)
        {
            throw new NUnitGenerationBuildException(ex.Message, ex);
        }
    }

    internal static void ValidateNUnitFrameworkVersion(string frameworkPath, string? sourceOutputDirectory = null) =>
        NUnitGenerationPolicy.ValidateNUnitFrameworkVersion(frameworkPath, sourceOutputDirectory);

    internal static bool IsVolatileGenerationOutput(string relativePath) => NUnitGenerationPaths.IsVolatileGenerationOutput(relativePath);
    internal static string NormalizeRelativePath(string relativePath) => NUnitGenerationPaths.NormalizeRelativePath(relativePath);
}
