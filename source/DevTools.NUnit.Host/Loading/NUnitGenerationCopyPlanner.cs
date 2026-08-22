using DevTools.Testing.Host.Loading;

namespace DevTools.NUnit.Host.Loading;

internal sealed record GenerationCopyEntry(string SourcePath, string RelativePath);

internal static class NUnitGenerationCopyPlanner
{
    internal static IReadOnlyList<GenerationCopyEntry> Create(
        string sourceAssemblyPath,
        string sourceOutputDirectory,
        HostRuntimeSource runtimeSource)
    {
        var outputFiles = Directory.EnumerateFiles(sourceOutputDirectory, "*", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ValidateNUnitFramework(outputFiles, sourceOutputDirectory);

        var copyEntries = new List<GenerationCopyEntry>();
        var foundFramework = false;

        foreach (var sourceFile in outputFiles)
        {
            if (!TryIncludeOutputFile(
                    sourceFile,
                    sourceOutputDirectory,
                    out var relativePath,
                    out var managedSimpleName))
            {
                continue;
            }

            if (managedSimpleName is not null && IsFrameworkSimpleName(managedSimpleName))
                foundFramework = true;

            copyEntries.Add(new GenerationCopyEntry(sourceFile, relativePath));
        }

        if (!foundFramework)
        {
            throw new NUnitGenerationBuildException(
                $"Exactly one {NUnitGenerationPolicy.FrameworkAssemblyFileName} is required in the test output directory.");
        }

        AppendRuntime(runtimeSource, copyEntries);
        return copyEntries;
    }

    private static bool TryIncludeOutputFile(
        string sourceFile,
        string sourceOutputDirectory,
        out string relativePath,
        out string? managedSimpleName)
    {
        relativePath = TestingGenerationPaths.NormalizeRelativePath(
            TestingGenerationPaths.GetRelativePath(sourceOutputDirectory, sourceFile));
        managedSimpleName = null;

        if (TestingGenerationPaths.IsVolatileGenerationOutput(relativePath))
            return false;

        if (TestingGenerationFiles.TryGetManagedAssemblyIdentity(sourceFile, out var simpleName))
        {
            if (TestingGenerationFiles.IsSharedTestingContract(sourceFile))
                return false;

            managedSimpleName = simpleName;
        }

        return true;
    }

    private static void AppendRuntime(
        HostRuntimeSource runtimeSource,
        List<GenerationCopyEntry> copyEntries)
    {
        copyEntries.Add(new GenerationCopyEntry(
            runtimeSource.AssemblyPath,
            NUnitGenerationPolicy.RuntimeAssemblyFileName));

        if (!string.IsNullOrWhiteSpace(runtimeSource.SymbolPath))
        {
            copyEntries.Add(new GenerationCopyEntry(
                runtimeSource.SymbolPath!,
                NUnitGenerationPolicy.RuntimeSymbolFileName));
        }

        foreach (var dependencyPath in runtimeSource.DependencyPaths)
            MergeRuntimeDependency(dependencyPath, copyEntries);
    }

    private static void MergeRuntimeDependency(
        string dependencyPath,
        List<GenerationCopyEntry> copyEntries)
    {
        var relativePath = Path.GetFileName(dependencyPath);
        if (IsRuntimeOwnedFileName(relativePath))
            return;

        var existingIndex = copyEntries.FindIndex(entry =>
            string.Equals(entry.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            // Runtime owns its private dependency closure. When the test output
            // already copied a different build of the same simple name (common for
            // net48 polyfills), keep the Runtime copy so Reflection.Metadata and
            // nunit.framework bind coherently with DevTools.NUnit.Runtime.
            if (!FilesHaveEqualContent(copyEntries[existingIndex].SourcePath, dependencyPath))
                copyEntries[existingIndex] = new GenerationCopyEntry(dependencyPath, relativePath);

            return;
        }

        copyEntries.Add(new GenerationCopyEntry(dependencyPath, relativePath));
    }

    private static bool IsRuntimeOwnedFileName(string relativePath) =>
        string.Equals(relativePath, NUnitGenerationPolicy.RuntimeAssemblyFileName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(relativePath, NUnitGenerationPolicy.RuntimeSymbolFileName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(relativePath, NUnitGenerationPolicy.FrameworkAssemblyFileName, StringComparison.OrdinalIgnoreCase);

    private static bool IsFrameworkSimpleName(string simpleName) =>
        string.Equals(
            simpleName,
            Path.GetFileNameWithoutExtension(NUnitGenerationPolicy.FrameworkAssemblyFileName),
            StringComparison.OrdinalIgnoreCase);

    private static void ValidateNUnitFramework(IReadOnlyList<string> outputFiles, string sourceOutputDirectory)
    {
        var frameworkMatches = outputFiles
            .Where(path => string.Equals(
                Path.GetFileName(path),
                NUnitGenerationPolicy.FrameworkAssemblyFileName,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (frameworkMatches.Count == 0)
        {
            throw new NUnitGenerationBuildException(
                $"Exactly one {NUnitGenerationPolicy.FrameworkAssemblyFileName} {NUnitGenerationPolicy.ExpectedNUnitPackageVersion} is required; none was found.");
        }

        if (frameworkMatches.Count > 1)
        {
            throw new NUnitGenerationBuildException(
                $"Exactly one {NUnitGenerationPolicy.FrameworkAssemblyFileName} {NUnitGenerationPolicy.ExpectedNUnitPackageVersion} is required; found {frameworkMatches.Count}.");
        }

        NUnitGenerationPolicy.ValidateNUnitFrameworkVersion(frameworkMatches[0], sourceOutputDirectory);
    }

    private static bool FilesHaveEqualContent(string firstPath, string secondPath)
    {
        var first = new FileInfo(firstPath);
        var second = new FileInfo(secondPath);
        if (first.Length != second.Length)
            return false;

        using var firstStream = File.OpenRead(firstPath);
        using var secondStream = File.OpenRead(secondPath);
        var firstBuffer = new byte[81920];
        var secondBuffer = new byte[81920];
        int firstRead;
        while ((firstRead = firstStream.Read(firstBuffer, 0, firstBuffer.Length)) > 0)
        {
            var secondRead = secondStream.Read(secondBuffer, 0, secondBuffer.Length);
            if (secondRead != firstRead
                || !firstBuffer.AsSpan(0, firstRead).SequenceEqual(secondBuffer.AsSpan(0, secondRead)))
            {
                return false;
            }
        }

        return secondStream.ReadByte() == -1;
    }
}
