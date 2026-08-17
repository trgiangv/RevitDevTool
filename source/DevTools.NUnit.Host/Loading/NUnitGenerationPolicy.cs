using System.Diagnostics;
using System.Reflection;
using DevTools.Testing.Host.Loading;

namespace DevTools.NUnit.Host.Loading;

/// <summary>
/// NUnit-owned description and validation of a runtime generation.  The common
/// store copies, hashes, and publishes this description without knowing any
/// NUnit file, version, or dependency rule.
/// </summary>
public sealed class NUnitGenerationPolicy : ITestingGenerationPolicy
{
    public const string RuntimeAssemblyFileName = "DevTools.NUnit.Runtime.dll";
    public const string RuntimeSymbolFileName = "DevTools.NUnit.Runtime.pdb";
    public const string FrameworkAssemblyFileName = "nunit.framework.dll";
    public const string FrameworkId = "nunit";

    internal const string ExpectedNUnitFileVersion = "4.6.1.0";
    internal const string ExpectedNUnitPackageVersion = "4.6.1";

    private readonly NUnitRuntimeSourcePathProvider _runtimeSourcePathProvider;

    public NUnitGenerationPolicy(NUnitRuntimeSourcePathProvider runtimeSourcePathProvider)
    {
        _runtimeSourcePathProvider = runtimeSourcePathProvider
            ?? throw new ArgumentNullException(nameof(runtimeSourcePathProvider));
    }

    public TestingGenerationPlan CreatePlan(string testAssemblyPath)
    {
        var sourceAssemblyPath = ResolveTestAssembly(testAssemblyPath);
        var sourceDirectory = Path.GetDirectoryName(sourceAssemblyPath)
            ?? throw new NUnitGenerationBuildException($"Test assembly path has no directory: {sourceAssemblyPath}");
        var runtime = ResolveRuntimeSource();
        var copyPlan = NUnitGenerationCopyPlanner.Create(sourceAssemblyPath, sourceDirectory, runtime);

        var files = copyPlan.CopyEntries
            .Select(entry => new TestingGenerationFile(
                entry.SourcePath,
                entry.RelativePath,
                Classify(entry.SourcePath)))
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
        var fileVersion = FileVersionInfo.GetVersionInfo(frameworkPath).FileVersion;
        if (!string.Equals(fileVersion, ExpectedNUnitFileVersion, StringComparison.Ordinal))
        {
            var location = sourceOutputDirectory is null
                ? frameworkPath
                : NUnitGenerationPaths.NormalizeRelativePath(NUnitGenerationPaths.GetRelativePath(sourceOutputDirectory, frameworkPath));
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
                $"{FrameworkAssemblyFileName} is not a valid managed assembly: {frameworkPath}", ex);
        }
    }

    private NUnitRuntimeSource ResolveRuntimeSource()
    {
        var source = _runtimeSourcePathProvider();
        if (string.IsNullOrWhiteSpace(source.AssemblyPath))
            throw new NUnitGenerationBuildException("Runtime assembly path provider returned an empty path.");

        var assemblyPath = Path.GetFullPath(source.AssemblyPath);
        if (!File.Exists(assemblyPath))
            throw new NUnitGenerationBuildException($"Runtime assembly not found: {assemblyPath}");

        string? symbolPath = null;
        if (!string.IsNullOrWhiteSpace(source.SymbolPath))
        {
            symbolPath = Path.GetFullPath(source.SymbolPath);
            if (!File.Exists(symbolPath))
                throw new NUnitGenerationBuildException($"Runtime symbol file not found: {symbolPath}");
        }

        var dependencies = source.DependencyPaths.Select(Path.GetFullPath).ToList();
        foreach (var dependency in dependencies)
        {
            if (!File.Exists(dependency))
                throw new NUnitGenerationBuildException($"Runtime dependency not found: {dependency}");
        }

        return new NUnitRuntimeSource(assemblyPath, symbolPath, dependencies);
    }

    private static string ResolveTestAssembly(string testAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(testAssemblyPath))
            throw new ArgumentException("Test assembly path is required.", nameof(testAssemblyPath));
        var sourceAssemblyPath = Path.GetFullPath(testAssemblyPath);
        if (!File.Exists(sourceAssemblyPath))
            throw new NUnitGenerationBuildException($"Test assembly not found: {sourceAssemblyPath}");
        return sourceAssemblyPath;
    }

    private static TestingGenerationFileKind Classify(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".pdb", StringComparison.OrdinalIgnoreCase))
            return TestingGenerationFileKind.Symbols;
        if (NUnitSharedAssemblyPolicy.TryGetManagedAssemblyIdentity(path, out _))
            return TestingGenerationFileKind.Managed;
        return string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase)
            ? TestingGenerationFileKind.Native
            : TestingGenerationFileKind.Other;
    }
}
