using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace DevTools.NUnit.Host.Loading;

public class NUnitGenerationBuildException : Exception
{
    public NUnitGenerationBuildException(string message)
        : base(message)
    {
    }

    public NUnitGenerationBuildException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class NUnitGenerationCorruptionException : NUnitGenerationBuildException
{
    public NUnitGenerationCorruptionException(
        string shadowDirectory,
        string expectedGenerationId,
        string actualGenerationId)
        : base(
            $"Published generation at '{shadowDirectory}' is corrupted: expected generation ID '{expectedGenerationId}', actual content hash '{actualGenerationId}'.")
    {
        ShadowDirectory = shadowDirectory;
        ExpectedGenerationId = expectedGenerationId;
        ActualGenerationId = actualGenerationId;
    }

    public string ShadowDirectory { get; }

    public string ExpectedGenerationId { get; }

    public string ActualGenerationId { get; }
}

public sealed record NUnitRuntimeSource(
    string AssemblyPath,
    string? SymbolPath,
    IReadOnlyList<string> DependencyPaths);

public delegate NUnitRuntimeSource NUnitRuntimeSourcePathProvider();

internal enum SnapshotCopyPhase
{
    BeforeCopy,
    AfterCopy,
}

public sealed class NUnitGenerationBuilder : INUnitGenerationBuilder
{
    // Generic snapshot/hash/publish lives in Testing.Host. This builder keeps
    // NUnit 4.6.1 framework-version validation and NUnitRuntime asset policy.
    public const string RuntimeAssemblyFileName = "DevTools.NUnit.Runtime.dll";
    public const string RuntimeSymbolFileName = "DevTools.NUnit.Runtime.pdb";
    public const string FrameworkAssemblyFileName = "nunit.framework.dll";
    public const string GenerationCompleteMarkerFileName = ".generation-complete";

    internal const string ExpectedNUnitFileVersion = "4.6.1.0";
    internal const string ExpectedNUnitPackageVersion = "4.6.1";

    private const int MaxSnapshotAttempts = 3;

    private static readonly ConcurrentDictionary<string, object> GenerationLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly NUnitRuntimeSourcePathProvider _runtimeSourcePathProvider;
    private readonly string _generationsRootDirectory;

    internal Action? AfterSnapshotBeforePublishHook { get; set; }

    internal Action<string, SnapshotCopyPhase>? SnapshotCopyProgressHook { get; set; }

    public NUnitGenerationBuilder(
        NUnitRuntimeSourcePathProvider runtimeSourcePathProvider,
        string? generationsRootDirectory = null)
    {
        _runtimeSourcePathProvider = runtimeSourcePathProvider ?? throw new ArgumentNullException(nameof(runtimeSourcePathProvider));
        _generationsRootDirectory = generationsRootDirectory
            ?? Path.Combine(Path.GetTempPath(), "DevTools", "NUnit", "Generations");
    }

    public NUnitGenerationManifest Build(string testAssemblyPath)
    {
        var source = ResolveTestAssembly(testAssemblyPath);
        var runtimeSource = ResolveRuntimeSource();
        string? lastFailure = null;

        for (var attempt = 0; attempt < MaxSnapshotAttempts; attempt++)
        {
            if (TryBuildAttempt(source, runtimeSource, out var manifest, out lastFailure))
                return manifest!;
        }

        var detail = string.IsNullOrWhiteSpace(lastFailure)
            ? string.Empty
            : $" Last failure: {lastFailure}.";
        throw new NUnitGenerationBuildException(
            $"Failed to create a coherent generation snapshot after {MaxSnapshotAttempts} attempts.{detail}");
    }

    private bool TryBuildAttempt(
        ResolvedTestAssembly source,
        NUnitRuntimeSource runtimeSource,
        out NUnitGenerationManifest? manifest,
        out string? failure)
    {
        manifest = null;
        failure = null;
        var stagingDirectory = CreateUniqueStagingDirectory();
        try
        {
            var copyPlan = NUnitGenerationCopyPlanner.Create(
                source.AssemblyPath,
                source.OutputDirectory,
                runtimeSource);

            if (!NUnitGenerationSnapshot.TryCopy(stagingDirectory, copyPlan, SnapshotCopyProgressHook))
            {
                failure = "source files changed during copy";
                return false;
            }

            if (!NUnitGenerationSnapshot.ValidateStaged(stagingDirectory, copyPlan))
            {
                failure = "staged test assembly is not a valid managed module";
                return false;
            }

            var generationId = NUnitGenerationSnapshot.ComputeGenerationId(
                stagingDirectory,
                copyPlan.ContentRelativePaths);
            if (!NUnitGenerationSnapshot.MatchesGenerationId(
                    stagingDirectory,
                    copyPlan.ContentRelativePaths,
                    generationId))
            {
                failure = "snapshot hash changed before publish";
                return false;
            }

            AfterSnapshotBeforePublishHook?.Invoke();

            if (!NUnitGenerationSnapshot.MatchesGenerationId(
                    stagingDirectory,
                    copyPlan.ContentRelativePaths,
                    generationId))
            {
                failure = "snapshot hash changed after the publish gate";
                return false;
            }

            return TryPublishGeneration(
                stagingDirectory,
                copyPlan,
                generationId,
                source.AssemblyPath,
                out manifest,
                out failure);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
                Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    private bool TryPublishGeneration(
        string stagingDirectory,
        GenerationCopyPlan copyPlan,
        string generationId,
        string sourceAssemblyPath,
        out NUnitGenerationManifest? manifest,
        out string? failure)
    {
        manifest = null;
        failure = null;
        var shadowDirectory = Path.Combine(_generationsRootDirectory, generationId);
        var generationLock = GenerationLocks.GetOrAdd(generationId, static _ => new object());
        lock (generationLock)
        {
            if (Directory.Exists(shadowDirectory) && NUnitGenerationSnapshot.IsPublished(shadowDirectory))
            {
                NUnitGenerationSnapshot.EnsurePublishedIsValid(shadowDirectory, generationId);
                manifest = NUnitGenerationManifestFactory.FromPublishedSnapshot(
                    generationId,
                    sourceAssemblyPath,
                    shadowDirectory);
                return true;
            }

            if (!NUnitGenerationSnapshot.MatchesGenerationId(
                    stagingDirectory,
                    copyPlan.ContentRelativePaths,
                    generationId))
            {
                failure = "snapshot hash changed while publishing";
                return false;
            }

            NUnitGenerationSnapshot.Publish(stagingDirectory, shadowDirectory, generationId);
            NUnitGenerationSnapshot.EnsurePublishedIsValid(shadowDirectory, generationId);
            manifest = NUnitGenerationManifestFactory.FromPublishedSnapshot(
                generationId,
                sourceAssemblyPath,
                shadowDirectory);
            return true;
        }
    }

    private static ResolvedTestAssembly ResolveTestAssembly(string testAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(testAssemblyPath))
            throw new ArgumentException("Test assembly path is required.", nameof(testAssemblyPath));

        var sourceAssemblyPath = Path.GetFullPath(testAssemblyPath);
        if (!File.Exists(sourceAssemblyPath))
        {
            throw new NUnitGenerationBuildException(
                $"Test assembly not found: {sourceAssemblyPath}");
        }

        var sourceOutputDirectory = Path.GetDirectoryName(sourceAssemblyPath)
            ?? throw new NUnitGenerationBuildException(
                $"Test assembly path has no directory: {sourceAssemblyPath}");

        return new ResolvedTestAssembly(sourceAssemblyPath, sourceOutputDirectory);
    }

    private NUnitRuntimeSource ResolveRuntimeSource()
    {
        var source = _runtimeSourcePathProvider();
        var assemblyPath = source.AssemblyPath;
        var symbolPath = source.SymbolPath;
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new NUnitGenerationBuildException(
                "Runtime assembly path provider returned an empty path.");
        }

        var fullAssemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullAssemblyPath))
        {
            throw new NUnitGenerationBuildException(
                $"Runtime assembly not found: {fullAssemblyPath}");
        }

        string? fullSymbolPath = null;
        if (!string.IsNullOrWhiteSpace(symbolPath))
        {
            fullSymbolPath = Path.GetFullPath(symbolPath);
            if (!File.Exists(fullSymbolPath))
            {
                throw new NUnitGenerationBuildException(
                    $"Runtime symbol file not found: {fullSymbolPath}");
            }
        }

        var dependencyPaths = source.DependencyPaths
            .Select(Path.GetFullPath)
            .ToList();
        foreach (var dependencyPath in dependencyPaths)
        {
            if (!File.Exists(dependencyPath))
            {
                throw new NUnitGenerationBuildException(
                    $"Runtime dependency not found: {dependencyPath}");
            }
        }

        return new NUnitRuntimeSource(fullAssemblyPath, fullSymbolPath, dependencyPaths);
    }

    internal static void ValidateNUnitFrameworkVersion(string frameworkPath, string? sourceOutputDirectory = null)
    {
        var fileVersion = FileVersionInfo.GetVersionInfo(frameworkPath).FileVersion;
        if (!string.Equals(fileVersion, ExpectedNUnitFileVersion, StringComparison.Ordinal))
        {
            var location = sourceOutputDirectory is null
                ? frameworkPath
                : NormalizeRelativePath(GetRelativePath(sourceOutputDirectory, frameworkPath));

            throw new NUnitGenerationBuildException(
                $"Expected {FrameworkAssemblyFileName} file version {ExpectedNUnitFileVersion} (NUnit package {ExpectedNUnitPackageVersion}); found {fileVersion ?? "<missing>"} at {location}.");
        }

        try
        {
            AssemblyName.GetAssemblyName(frameworkPath);
        }
        catch (Exception ex)
        {
            throw new NUnitGenerationBuildException(
                $"{FrameworkAssemblyFileName} is not a valid managed assembly: {frameworkPath}",
                ex);
        }
    }

    private string CreateUniqueStagingDirectory()
    {
        var directory = Path.Combine(
            _generationsRootDirectory,
            ".staging." + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        return directory;
    }

    internal static bool IsVolatileGenerationOutput(string relativePath) =>
        NUnitGenerationPaths.IsVolatileGenerationOutput(relativePath);

    internal static string NormalizeRelativePath(string relativePath) =>
        NUnitGenerationPaths.NormalizeRelativePath(relativePath);

    private static string GetRelativePath(string relativeTo, string path) =>
        NUnitGenerationPaths.GetRelativePath(relativeTo, path);

    private sealed record ResolvedTestAssembly(string AssemblyPath, string OutputDirectory);
}
