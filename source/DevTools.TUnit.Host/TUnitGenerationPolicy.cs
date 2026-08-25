using DevTools.Testing.Host.Loading;

namespace DevTools.TUnit.Host;

public sealed class TUnitGenerationPolicy(Func<HostRuntimeSource> runtimeSourceProvider) : ITestingGenerationPolicy
{
    public const string FrameworkId = "tunit";
    private const string FrameworkAssemblyFileName = "TUnit.Core.dll";
    public const string RuntimeFolderName = "TUnitRuntime";
    public const string RuntimeAssemblyFileName = "DevTools.TUnit.Runtime.dll";
    internal const string ExpectedTUnitFileVersion = "1.65.63.0";
    internal const string ExpectedTUnitPackageVersion = "1.65.63";

    public TestingGenerationPlan CreatePlan(string testAssemblyPath)
    {
        var assemblyPath = Path.GetFullPath(testAssemblyPath);
        if (!File.Exists(assemblyPath))
            throw new TestingGenerationBuildException($"TUnit test assembly not found: {assemblyPath}");

        var outputDirectory = Path.GetDirectoryName(assemblyPath)!;
        var files = TestingGenerationFiles.ScanOutputDirectory(outputDirectory);

        if (!files.Keys.Any(filePath => string.Equals(
                Path.GetFileName(filePath),
                FrameworkAssemblyFileName,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new TestingGenerationBuildException(
                $"{FrameworkAssemblyFileName} was not found beside the TUnit test assembly.");
        }

        var runtime = HostRuntimeSources.Normalize(
            runtimeSourceProvider(),
            static message => new TestingGenerationBuildException(message));
        AddRuntimeFile(files, runtime.AssemblyPath, RuntimeAssemblyFileName);
        foreach (var dependency in runtime.DependencyPaths)
            AddRuntimeFile(files, dependency, Path.GetFileName(dependency));

        return new TestingGenerationPlan(
            FrameworkId,
            assemblyPath,
            files.Values.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase).ToList(),
            RuntimeAssemblyFileName);
    }

    public void ValidatePublished(TestingGenerationManifest manifest)
    {
        if (!string.Equals(manifest.FrameworkId, FrameworkId, StringComparison.OrdinalIgnoreCase))
            throw new TestingGenerationBuildException($"Expected TUnit generation framework ID '{FrameworkId}'.");
        if (!File.Exists(manifest.RuntimeAssemblyPath))
            throw new TestingGenerationBuildException($"Published TUnit runtime is missing: {manifest.RuntimeAssemblyPath}");
        if (!manifest.ManagedAssemblies.Any(path => string.Equals(
                Path.GetFileName(path),
                FrameworkAssemblyFileName,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new TestingGenerationBuildException(
                $"Published TUnit generation is missing {FrameworkAssemblyFileName}.");
        }

        var frameworks = manifest.ManagedAssemblies
            .Where(path => string.Equals(
                Path.GetFileName(path),
                FrameworkAssemblyFileName,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (frameworks.Count != 1)
        {
            throw new TestingGenerationBuildException(
                $"Exactly one {FrameworkAssemblyFileName} {ExpectedTUnitPackageVersion} is required in the published generation; found {frameworks.Count}.");
        }

        ValidateTUnitFrameworkVersion(frameworks[0], manifest.ShadowDirectory);
    }

    internal static void ValidateTUnitFrameworkVersion(string frameworkPath, string? sourceOutputDirectory = null)
    {
        TestingGenerationFiles.TryGetFileVersion(frameworkPath, out var fileVersion);
        if (!string.Equals(fileVersion, ExpectedTUnitFileVersion, StringComparison.Ordinal))
        {
            var location = sourceOutputDirectory is null
                ? frameworkPath
                : TestingGenerationFiles.NormalizeRelativePath(
                    TestingGenerationFiles.GetRelativePath(sourceOutputDirectory, frameworkPath));
            throw new TestingGenerationBuildException(
                $"Expected {FrameworkAssemblyFileName} file version {ExpectedTUnitFileVersion} (package {ExpectedTUnitPackageVersion}); found {fileVersion ?? "<missing>"} at {location}.");
        }

        if (!TestingGenerationFiles.IsManagedAssembly(frameworkPath))
        {
            throw new TestingGenerationBuildException(
                $"{FrameworkAssemblyFileName} is not a valid managed assembly: {frameworkPath}");
        }
    }

    private static void AddRuntimeFile(
        IDictionary<string, TestingGenerationFile> files,
        string sourcePath,
        string relativePath)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(sourcePath))
            throw new TestingGenerationBuildException($"TUnit runtime dependency not found: {sourcePath}");
        if (TestingGenerationFiles.IsSharedTestingContract(sourcePath))
            return;

        TestingGenerationFiles.MergeFile(files, sourcePath, relativePath);
    }
}
