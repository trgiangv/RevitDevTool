using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Host.Loading;

/// <summary>
/// A generation made only of the user's output: the framework runtime the adapter copied
/// there and the user's framework closure. The compatibility stops run before any copy.
/// </summary>
public sealed class OutputGeneration(TestFrameworkId frameworkId) : ITestingGenerationPolicy
{
    public TestFrameworkId FrameworkId => frameworkId;

    public TestingGenerationPlan CreatePlan(string testAssemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testAssemblyPath);
        var assemblyPath = Path.GetFullPath(testAssemblyPath);
        if (!File.Exists(assemblyPath))
            throw new TestingGenerationBuildException($"{frameworkId} test assembly not found: {assemblyPath}");

        var outputDirectory = Path.GetDirectoryName(assemblyPath)
            ?? throw new TestingGenerationBuildException($"Test assembly path has no directory: {assemblyPath}");
        RuntimeCompatChecker.Check(frameworkId, outputDirectory);

        return new TestingGenerationPlan(
            frameworkId,
            assemblyPath,
            TestingGenerationFiles.ScanOutputDirectory(outputDirectory).Values
                .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Path.ChangeExtension(RuntimeManifest.FileName(frameworkId), ".dll"));
    }

    public void ValidatePublished(TestingGenerationManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.FrameworkId != frameworkId)
            throw new TestingGenerationBuildException($"Expected {frameworkId} generation framework ID '{frameworkId}'.");
        if (!File.Exists(manifest.RuntimeAssemblyPath))
            throw new TestingGenerationBuildException($"Published {frameworkId} runtime is missing: {manifest.RuntimeAssemblyPath}");
    }
}
