using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Host.Loading;

/// <summary>
/// File names and version pins for one framework. Not a policy: NUnit, TUnit,
/// and MSTest are the <see cref="ITestingGenerationPolicy"/> implementations.
/// </summary>
internal sealed record TestingGenerationSpec(
    TestFrameworkId FrameworkId,
    string FrameworkName,
    string FrameworkAssemblyFileName,
    string RuntimeAssemblyFileName,
    string? RuntimeSymbolFileName,
    IReadOnlyList<GenerationPin> Pins,
    bool RuntimeOwnsDependencyClosure)
{
    public TestingGenerationPlan CreatePlan(
        Func<RuntimeSource> runtimeSourceProvider,
        string testAssemblyPath)
    {
        ArgumentNullException.ThrowIfNull(runtimeSourceProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(testAssemblyPath);
        var assemblyPath = Path.GetFullPath(testAssemblyPath);
        if (!File.Exists(assemblyPath))
            throw new TestingGenerationBuildException($"{FrameworkName} test assembly not found: {assemblyPath}");

        var outputDirectory = Path.GetDirectoryName(assemblyPath)
            ?? throw new TestingGenerationBuildException($"Test assembly path has no directory: {assemblyPath}");
        var files = TestingGenerationFiles.ScanOutputDirectory(outputDirectory);

        if (!files.Keys.Any(filePath => TestingGenerationPins.IsNamed(filePath, FrameworkAssemblyFileName)))
        {
            throw new TestingGenerationBuildException(
                $"{FrameworkAssemblyFileName} was not found beside the {FrameworkName} test assembly.");
        }

        TestingGenerationPins.ValidateConsumerPins(
            files.Values.Select(file => file.SourcePath),
            outputDirectory,
            Pins);

        var runtime = runtimeSourceProvider().RequirePresent(
            static message => new TestingGenerationBuildException(message));
        AppendRuntime(runtime, files);

        return new TestingGenerationPlan(
            FrameworkId,
            assemblyPath,
            files.Values.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase).ToList(),
            RuntimeAssemblyFileName);
    }

    public void ValidatePublished(TestingGenerationManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.FrameworkId != FrameworkId)
            throw new TestingGenerationBuildException($"Expected {FrameworkName} generation framework ID '{FrameworkId}'.");
        if (!File.Exists(manifest.RuntimeAssemblyPath))
            throw new TestingGenerationBuildException($"Published {FrameworkName} runtime is missing: {manifest.RuntimeAssemblyPath}");

        foreach (var pin in Pins)
        {
            var matches = manifest.ManagedAssemblies
                .Where(path => TestingGenerationPins.IsNamed(path, pin.FileName))
                .ToList();
            if (pin.AllowMany)
            {
                if (matches.Count == 0)
                {
                    throw new TestingGenerationBuildException(
                        $"Published {FrameworkName} generation is missing {pin.FileName} {pin.CountLabel}.");
                }
            }
            else if (matches.Count != 1)
            {
                throw new TestingGenerationBuildException(
                    $"Exactly one {pin.FileName} {pin.CountLabel} is required in the published generation; found {matches.Count}.");
            }

            foreach (var match in matches)
                pin.Validate(match, manifest.ShadowDirectory);
        }
    }

    private void AppendRuntime(
        RuntimeSource runtime,
        IDictionary<string, (string SourcePath, string RelativePath)> files)
    {
        AddRuntimeFile(files, runtime.AssemblyPath, RuntimeAssemblyFileName);
        if (RuntimeSymbolFileName is not null && !string.IsNullOrWhiteSpace(runtime.SymbolPath))
            AddRuntimeFile(files, runtime.SymbolPath!, RuntimeSymbolFileName);

        foreach (var dependency in runtime.DependencyPaths)
        {
            var relativePath = Path.GetFileName(dependency);
            if (RuntimeOwnsDependencyClosure && IsRuntimeOwnedFileName(relativePath))
                continue;

            AddRuntimeFile(files, dependency, relativePath);
        }
    }

    private void AddRuntimeFile(
        IDictionary<string, (string SourcePath, string RelativePath)> files,
        string sourcePath,
        string relativePath)
    {
        if (!RuntimeOwnsDependencyClosure && TestingGenerationFiles.IsSharedTestingContract(sourcePath))
            return;

        TestingGenerationFiles.MergeRuntimeDependency(files, sourcePath, relativePath);
    }

    private bool IsRuntimeOwnedFileName(string relativePath) =>
        string.Equals(relativePath, RuntimeAssemblyFileName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(relativePath, RuntimeSymbolFileName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(relativePath, FrameworkAssemblyFileName, StringComparison.OrdinalIgnoreCase);
}
