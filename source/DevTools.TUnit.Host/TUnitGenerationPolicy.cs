using System.Reflection;
using DevTools.Testing.Host.Loading;

namespace DevTools.TUnit.Host;

public sealed class TUnitGenerationPolicy(Func<HostRuntimeSource> runtimeSourceProvider) : ITestingGenerationPolicy
{
    public const string FrameworkId = "tunit";
    private const string FrameworkAssemblyFileName = "TUnit.Core.dll";
    internal const string PlatformAssemblyFileName = "Microsoft.Testing.Platform.dll";
    public const string RuntimeFolderName = "TUnitRuntime";
    public const string RuntimeAssemblyFileName = "DevTools.TUnit.Runtime.dll";
    internal static readonly Version ExpectedTUnitAssemblyVersion = new(1, 66, 27, 0);
    internal static readonly Version ExpectedMtpAssemblyVersion = new(2, 4, 0, 0);

    public TestingGenerationPlan CreatePlan(string testAssemblyPath)
    {
        var assemblyPath = Path.GetFullPath(testAssemblyPath);
        if (!File.Exists(assemblyPath))
            throw new TestingGenerationBuildException($"TUnit test assembly not found: {assemblyPath}");

        var outputDirectory = Path.GetDirectoryName(assemblyPath)!;
        var files = TestingGenerationFiles.ScanOutputDirectory(outputDirectory);

        if (!files.Keys.Any(filePath => IsNamed(filePath, FrameworkAssemblyFileName)))
        {
            throw new TestingGenerationBuildException(
                $"{FrameworkAssemblyFileName} was not found beside the TUnit test assembly.");
        }

        ValidateConsumerPins(files.Values.Select(file => file.SourcePath), outputDirectory);

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

        var frameworks = manifest.ManagedAssemblies
            .Where(path => IsNamed(path, FrameworkAssemblyFileName))
            .ToList();
        if (frameworks.Count != 1)
        {
            throw new TestingGenerationBuildException(
                $"Exactly one {FrameworkAssemblyFileName} {ExpectedTUnitAssemblyVersion} is required in the published generation; found {frameworks.Count}.");
        }

        ValidateTUnitFrameworkVersion(frameworks[0], manifest.ShadowDirectory);

        var platforms = manifest.ManagedAssemblies
            .Where(path => IsNamed(path, PlatformAssemblyFileName))
            .ToList();
        if (platforms.Count == 0)
        {
            throw new TestingGenerationBuildException(
                $"Published TUnit generation is missing {PlatformAssemblyFileName} {ExpectedMtpAssemblyVersion}.");
        }

        foreach (var platform in platforms)
            ValidateMtpAssemblyVersion(platform, manifest.ShadowDirectory);
    }

    internal static void ValidateTUnitFrameworkVersion(string frameworkPath, string? sourceOutputDirectory = null) =>
        ValidateAssemblyVersion(
            frameworkPath,
            FrameworkAssemblyFileName,
            ExpectedTUnitAssemblyVersion,
            sourceOutputDirectory);

    internal static void ValidateMtpAssemblyVersion(string platformPath, string? sourceOutputDirectory = null) =>
        ValidateAssemblyVersion(
            platformPath,
            PlatformAssemblyFileName,
            ExpectedMtpAssemblyVersion,
            sourceOutputDirectory);

    private static void ValidateAssemblyVersion(
        string path,
        string fileName,
        Version expected,
        string? sourceOutputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Version? version = null;
        try
        {
            version = AssemblyName.GetAssemblyName(path).Version;
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException)
        {
            throw new TestingGenerationBuildException(
                $"{fileName} is not a valid managed assembly: {path}");
        }

        if (version != expected)
        {
            throw new TestingGenerationBuildException(
                $"Expected {fileName} {expected}; found {version?.ToString() ?? "<missing>"} at {Describe(path, sourceOutputDirectory)}.");
        }
    }

    private static void ValidateConsumerPins(IEnumerable<string> paths, string outputDirectory)
    {
        foreach (var path in paths)
        {
            if (IsNamed(path, FrameworkAssemblyFileName))
                ValidateTUnitFrameworkVersion(path, outputDirectory);
            else if (IsNamed(path, PlatformAssemblyFileName))
                ValidateMtpAssemblyVersion(path, outputDirectory);
        }
    }

    private static bool IsNamed(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);

    private static string Describe(string path, string? sourceOutputDirectory) =>
        sourceOutputDirectory is null
            ? path
            : TestingGenerationFiles.NormalizeRelativePath(
                TestingGenerationFiles.GetRelativePath(sourceOutputDirectory, path));

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
