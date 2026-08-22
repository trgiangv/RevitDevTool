using DevTools.Testing.Host.Loading;

namespace DevTools.NUnit.Host.Loading;

/// <summary>
/// NUnit-owned description and validation of a runtime generation.  The common
/// store copies, hashes, and publishes this description without knowing any
/// NUnit file, version, or dependency rule.
/// </summary>
public sealed class NUnitGenerationPolicy : ITestingGenerationPolicy
{
    public const string FrameworkId = "nunit";
    public const string FrameworkAssemblyFileName = "nunit.framework.dll";
    public const string RuntimeFolderName = "NUnitRuntime";
    public const string RuntimeAssemblyFileName = "DevTools.NUnit.Runtime.dll";
    public const string RuntimeSymbolFileName = "DevTools.NUnit.Runtime.pdb";

    internal const string ExpectedNUnitFileVersion = "4.6.1.0";
    internal const string ExpectedNUnitPackageVersion = "4.6.1";

    private readonly Func<HostRuntimeSource> _runtimeSourceProvider;

    public NUnitGenerationPolicy(Func<HostRuntimeSource> runtimeSourceProvider)
    {
        _runtimeSourceProvider = runtimeSourceProvider
            ?? throw new ArgumentNullException(nameof(runtimeSourceProvider));
    }

    public TestingGenerationPlan CreatePlan(string testAssemblyPath)
    {
        var sourceAssemblyPath = ResolveTestAssembly(testAssemblyPath);
        var sourceDirectory = Path.GetDirectoryName(sourceAssemblyPath)
            ?? throw new NUnitGenerationBuildException($"Test assembly path has no directory: {sourceAssemblyPath}");
        var runtime = HostRuntimeSources.Normalize(
            _runtimeSourceProvider(),
            static message => new NUnitGenerationBuildException(message));
        var copyEntries = NUnitGenerationCopyPlanner.Create(sourceAssemblyPath, sourceDirectory, runtime);

        var files = copyEntries
            .Select(entry => new TestingGenerationFile(
                entry.SourcePath,
                entry.RelativePath,
                TestingGenerationFiles.Classify(entry.SourcePath)))
            .ToList();

        return new TestingGenerationPlan(
            FrameworkId,
            sourceAssemblyPath,
            files,
            RuntimeAssemblyFileName);
    }

    public void ValidatePublished(TestingGenerationManifest manifest)
    {
        if (manifest is null)
            throw new ArgumentNullException(nameof(manifest));
        if (!string.Equals(manifest.FrameworkId, FrameworkId, StringComparison.OrdinalIgnoreCase))
            throw new NUnitGenerationBuildException($"Expected NUnit generation framework ID '{FrameworkId}'.");

        var frameworks = manifest.ManagedAssemblies
            .Where(path => string.Equals(Path.GetFileName(path), FrameworkAssemblyFileName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (frameworks.Count != 1)
        {
            throw new NUnitGenerationBuildException(
                $"Exactly one {FrameworkAssemblyFileName} {ExpectedNUnitPackageVersion} is required in the published generation; found {frameworks.Count}.");
        }

        ValidateNUnitFrameworkVersion(frameworks[0], manifest.ShadowDirectory);
        if (!File.Exists(manifest.RuntimeAssemblyPath))
            throw new NUnitGenerationBuildException($"Published NUnit runtime assembly is missing: {manifest.RuntimeAssemblyPath}");
    }

    internal static void ValidateNUnitFrameworkVersion(string frameworkPath, string? sourceOutputDirectory = null)
    {
        TestingGenerationFiles.TryGetFileVersion(frameworkPath, out var fileVersion);
        if (!string.Equals(fileVersion, ExpectedNUnitFileVersion, StringComparison.Ordinal))
        {
            var location = sourceOutputDirectory is null
                ? frameworkPath
                : TestingGenerationFiles.NormalizeRelativePath(
                    TestingGenerationFiles.GetRelativePath(sourceOutputDirectory, frameworkPath));
            throw new NUnitGenerationBuildException(
                $"Expected {FrameworkAssemblyFileName} file version {ExpectedNUnitFileVersion} (package {ExpectedNUnitPackageVersion}); found {fileVersion ?? "<missing>"} at {location}.");
        }

        if (!TestingGenerationFiles.IsManagedAssembly(frameworkPath))
        {
            throw new NUnitGenerationBuildException(
                $"{FrameworkAssemblyFileName} is not a valid managed assembly: {frameworkPath}");
        }
    }

    internal static string GetFrameworkAssemblyPath(TestingGenerationManifest manifest) =>
        manifest.ManagedAssemblies.Single(path =>
            string.Equals(Path.GetFileName(path), FrameworkAssemblyFileName, StringComparison.OrdinalIgnoreCase));

    private static string ResolveTestAssembly(string testAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(testAssemblyPath))
            throw new ArgumentException("Test assembly path is required.", nameof(testAssemblyPath));
        var sourceAssemblyPath = Path.GetFullPath(testAssemblyPath);
        if (!File.Exists(sourceAssemblyPath))
            throw new NUnitGenerationBuildException($"Test assembly not found: {sourceAssemblyPath}");
        return sourceAssemblyPath;
    }
}
