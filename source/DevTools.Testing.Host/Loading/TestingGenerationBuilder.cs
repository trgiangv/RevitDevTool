using System.Collections.Concurrent;

namespace DevTools.Testing.Host.Loading;

public sealed class TestingGenerationBuilder
{
    private static readonly ConcurrentDictionary<string, object> GenerationLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _generationsRootDirectory;

    public TestingGenerationBuilder(string? generationsRootDirectory = null)
    {
        _generationsRootDirectory = generationsRootDirectory
            ?? Path.Combine(Path.GetTempPath(), "DevTools", "Testing", "Generations");
    }

    public TestingGenerationManifest Build(TestingRuntimePayload payload)
    {
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));

        var sourceAssemblyPath = Path.GetFullPath(payload.TestAssemblyPath);
        if (!File.Exists(sourceAssemblyPath))
            throw new InvalidOperationException($"Test assembly not found: {sourceAssemblyPath}");

        var sourceOutputDirectory = Path.GetDirectoryName(sourceAssemblyPath)
            ?? throw new InvalidOperationException($"Test assembly path has no directory: {sourceAssemblyPath}");

        var runtimeAssemblyPath = RequireFile(payload.RuntimeAssemblyPath, "Runtime assembly");
        var frameworkAssemblyPath = RequireFile(payload.FrameworkAssemblyPath, "Framework assembly");

        var stagingDirectory = Path.Combine(
            _generationsRootDirectory,
            ".staging." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var copyEntries = CollectOutputFiles(sourceOutputDirectory);
            copyEntries.Add((runtimeAssemblyPath, Path.GetFileName(runtimeAssemblyPath)));
            MergeFile(copyEntries, frameworkAssemblyPath);

            foreach (var probeRoot in payload.AdditionalProbeRoots ?? Array.Empty<string>())
            {
                var fullProbe = Path.GetFullPath(probeRoot);
                if (File.Exists(fullProbe))
                    MergeFile(copyEntries, fullProbe);
                else if (Directory.Exists(fullProbe))
                    copyEntries.AddRange(CollectOutputFiles(fullProbe));
            }

            foreach (var (source, relative) in copyEntries)
                TestingGenerationSnapshot.CopyFile(source, Path.Combine(stagingDirectory, relative));

            var contentRelativePaths = copyEntries
                .Select(entry => entry.RelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var generationId = TestingGenerationSnapshot.ComputeGenerationId(
                stagingDirectory,
                contentRelativePaths);
            var shadowDirectory = Path.Combine(_generationsRootDirectory, generationId);
            var generationLock = GenerationLocks.GetOrAdd(generationId, static _ => new object());
            lock (generationLock)
            {
                if (!Directory.Exists(shadowDirectory)
                    || !File.Exists(Path.Combine(
                        shadowDirectory,
                        TestingGenerationPaths.GenerationCompleteMarkerFileName)))
                {
                    TestingGenerationSnapshot.Publish(stagingDirectory, shadowDirectory, generationId);
                }
            }

            return CreateManifest(
                generationId,
                payload.FrameworkId,
                sourceAssemblyPath,
                sourceOutputDirectory,
                shadowDirectory,
                Path.GetFileName(runtimeAssemblyPath),
                Path.GetFileName(frameworkAssemblyPath));
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
                Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    private static string RequireFile(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException($"{label} path is required.");

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new InvalidOperationException($"{label} not found: {fullPath}");

        return fullPath;
    }

    private static List<(string SourcePath, string RelativePath)> CollectOutputFiles(string sourceOutputDirectory)
    {
        var copyEntries = new List<(string SourcePath, string RelativePath)>();
        foreach (var sourceFile in Directory.EnumerateFiles(sourceOutputDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = TestingGenerationPaths.NormalizeRelativePath(
                TestingGenerationPaths.GetRelativePath(sourceOutputDirectory, sourceFile));
            if (TestingGenerationPaths.IsVolatileGenerationOutput(relativePath))
                continue;

            copyEntries.Add((Path.GetFullPath(sourceFile), relativePath));
        }

        return copyEntries;
    }

    private static void MergeFile(List<(string SourcePath, string RelativePath)> copyEntries, string sourcePath)
    {
        var relativePath = Path.GetFileName(sourcePath);
        var existing = copyEntries.FindIndex(entry =>
            string.Equals(entry.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
            copyEntries[existing] = (sourcePath, relativePath);
        else
            copyEntries.Add((sourcePath, relativePath));
    }

    private static TestingGenerationManifest CreateManifest(
        string generationId,
        string frameworkId,
        string sourceAssemblyPath,
        string sourceOutputDirectory,
        string shadowDirectory,
        string runtimeFileName,
        string frameworkFileName)
    {
        var sourceAssemblyRelativePath = TestingGenerationPaths.NormalizeRelativePath(
            TestingGenerationPaths.GetRelativePath(sourceOutputDirectory, sourceAssemblyPath));

        var managed = new List<string>();
        var native = new List<string>();
        foreach (var absolutePath in Directory.EnumerateFiles(shadowDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = TestingGenerationPaths.NormalizeRelativePath(
                TestingGenerationPaths.GetRelativePath(shadowDirectory, absolutePath));
            if (string.Equals(
                    relativePath,
                    TestingGenerationPaths.GenerationCompleteMarkerFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TestingGenerationSnapshot.TryGetManagedAssemblyName(absolutePath, out _))
                managed.Add(Path.Combine(shadowDirectory, relativePath));
            else if (string.Equals(Path.GetExtension(absolutePath), ".dll", StringComparison.OrdinalIgnoreCase))
                native.Add(Path.Combine(shadowDirectory, relativePath));
        }

        managed.Sort(StringComparer.OrdinalIgnoreCase);
        native.Sort(StringComparer.OrdinalIgnoreCase);

        return new TestingGenerationManifest(
            generationId,
            frameworkId,
            sourceAssemblyPath,
            shadowDirectory,
            Path.Combine(shadowDirectory, sourceAssemblyRelativePath),
            Path.Combine(shadowDirectory, runtimeFileName),
            Path.Combine(shadowDirectory, frameworkFileName),
            managed,
            native);
    }
}
