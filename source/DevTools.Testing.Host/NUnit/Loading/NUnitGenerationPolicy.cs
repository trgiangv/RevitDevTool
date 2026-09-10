using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.NUnit.Loading;

/// <summary>
/// NUnit-owned description and validation of a runtime generation. The store
/// copies, hashes, and publishes this description without knowing any NUnit
/// file, version, or dependency rule.
/// </summary>
public sealed class NUnitGenerationPolicy(Func<HostRuntimeSource> runtimeSourceProvider) : ITestingGenerationPolicy
{
    public const TestFrameworkId FrameworkId = TestFrameworkId.NUnit;
    public const string FrameworkAssemblyFileName = "nunit.framework.dll";
    public const string RuntimeFolderName = "NUnitRuntime";
    public const string RuntimeAssemblyFileName = "DevTools.NUnit.Runtime.dll";
    public const string RuntimeSymbolFileName = "DevTools.NUnit.Runtime.pdb";

    internal const string ExpectedNUnitFileVersion = "4.6.1.0";
    internal const string ExpectedNUnitPackageVersion = "4.6.1";

    private readonly Func<HostRuntimeSource> _runtimeSourceProvider = runtimeSourceProvider ?? throw new ArgumentNullException(nameof(runtimeSourceProvider));

    public TestingGenerationPlan CreatePlan(string testAssemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testAssemblyPath);
        var sourceAssemblyPath = Path.GetFullPath(testAssemblyPath);
        if (!File.Exists(sourceAssemblyPath))
            throw new TestingGenerationBuildException($"Test assembly not found: {sourceAssemblyPath}");

        var sourceDirectory = Path.GetDirectoryName(sourceAssemblyPath)
            ?? throw new TestingGenerationBuildException($"Test assembly path has no directory: {sourceAssemblyPath}");
        var runtime = HostRuntimeSources.Normalize(
            _runtimeSourceProvider(),
            static message => new TestingGenerationBuildException(message));

        ValidateNUnitFramework(sourceDirectory);
        var files = TestingGenerationFiles.ScanOutputDirectory(sourceDirectory);
        AppendRuntime(runtime, files);

        return new TestingGenerationPlan(
            FrameworkId,
            sourceAssemblyPath,
            files.Values.OrderBy(static file => file.RelativePath, StringComparer.OrdinalIgnoreCase).ToList(),
            RuntimeAssemblyFileName);
    }

    public void ValidatePublished(TestingGenerationManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.FrameworkId != FrameworkId)
            throw new TestingGenerationBuildException($"Expected NUnit generation framework ID '{FrameworkId}'.");

        var frameworks = manifest.ManagedAssemblies
            .Where(path => string.Equals(Path.GetFileName(path), FrameworkAssemblyFileName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (frameworks.Count != 1)
        {
            throw new TestingGenerationBuildException(
                $"Exactly one {FrameworkAssemblyFileName} {ExpectedNUnitPackageVersion} is required in the published generation; found {frameworks.Count}.");
        }

        ValidateNUnitFrameworkVersion(frameworks[0], manifest.ShadowDirectory);
        if (!File.Exists(manifest.RuntimeAssemblyPath))
            throw new TestingGenerationBuildException($"Published NUnit runtime assembly is missing: {manifest.RuntimeAssemblyPath}");
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
            throw new TestingGenerationBuildException(
                $"Expected {FrameworkAssemblyFileName} file version {ExpectedNUnitFileVersion} (package {ExpectedNUnitPackageVersion}); found {fileVersion ?? "<missing>"} at {location}.");
        }

        if (!TestingGenerationFiles.IsManagedAssembly(frameworkPath))
        {
            throw new TestingGenerationBuildException(
                $"{FrameworkAssemblyFileName} is not a valid managed assembly: {frameworkPath}");
        }
    }

    internal static string GetFrameworkAssemblyPath(TestingGenerationManifest manifest) =>
        manifest.ManagedAssemblies.Single(path =>
            string.Equals(Path.GetFileName(path), FrameworkAssemblyFileName, StringComparison.OrdinalIgnoreCase));

    private static void ValidateNUnitFramework(string sourceOutputDirectory)
    {
        var frameworkMatches = Directory.EnumerateFiles(
                sourceOutputDirectory,
                FrameworkAssemblyFileName,
                SearchOption.AllDirectories)
            .ToList();

        switch (frameworkMatches.Count)
        {
            case 0:
                throw new TestingGenerationBuildException(
                    $"Exactly one {FrameworkAssemblyFileName} {ExpectedNUnitPackageVersion} is required; none was found.");
            case > 1:
                throw new TestingGenerationBuildException(
                    $"Exactly one {FrameworkAssemblyFileName} {ExpectedNUnitPackageVersion} is required; found {frameworkMatches.Count}.");
            default:
                ValidateNUnitFrameworkVersion(frameworkMatches[0], sourceOutputDirectory);
                break;
        }

    }

    private static void AppendRuntime(
        HostRuntimeSource runtimeSource,
        IDictionary<string, TestingGenerationFile> files)
    {
        TestingGenerationFiles.MergeFile(files, runtimeSource.AssemblyPath, RuntimeAssemblyFileName);
        if (!string.IsNullOrWhiteSpace(runtimeSource.SymbolPath))
            TestingGenerationFiles.MergeFile(files, runtimeSource.SymbolPath!, RuntimeSymbolFileName);

        foreach (var dependencyPath in runtimeSource.DependencyPaths)
        {
            var relativePath = Path.GetFileName(dependencyPath);
            if (IsRuntimeOwnedFileName(relativePath))
                continue;

            // Runtime owns its private dependency closure. When the test output
            // already has a different build of the same simple name (common for
            // net48 polyfills), MergeFile keeps the Runtime copy so
            // Reflection.Metadata binds with DevTools.NUnit.Runtime.
            TestingGenerationFiles.MergeFile(files, dependencyPath, relativePath);
        }
    }

    private static bool IsRuntimeOwnedFileName(string relativePath) =>
        string.Equals(relativePath, RuntimeAssemblyFileName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(relativePath, RuntimeSymbolFileName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(relativePath, FrameworkAssemblyFileName, StringComparison.OrdinalIgnoreCase);
}
