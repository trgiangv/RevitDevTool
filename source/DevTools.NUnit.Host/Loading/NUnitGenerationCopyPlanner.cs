namespace DevTools.NUnit.Host.Loading;

internal sealed record GenerationCopyEntry(string SourcePath, string RelativePath);

internal sealed record GenerationCopyPlan(
    string SourceAssemblyRelativePath,
    string FrameworkRelativePath,
    IReadOnlyList<GenerationCopyEntry> CopyEntries,
    IReadOnlyList<string> ContentRelativePaths,
    IReadOnlyList<string> ManagedAssemblyRelativePaths);

internal static class NUnitGenerationCopyPlanner
{
    internal static GenerationCopyPlan Create(
        string sourceAssemblyPath,
        string sourceOutputDirectory,
        NUnitRuntimeSource runtimeSource)
    {
        var sourceAssemblyRelativePath = NUnitGenerationPaths.NormalizeRelativePath(
            NUnitGenerationPaths.GetRelativePath(sourceOutputDirectory, sourceAssemblyPath));

        var outputFiles = Directory.EnumerateFiles(sourceOutputDirectory, "*", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ValidateNUnitFramework(outputFiles, sourceOutputDirectory);

        var copyEntries = new List<GenerationCopyEntry>();
        var contentRelativePaths = new List<string>();
        var managedAssemblyRelativePaths = new List<string>();
        string? frameworkRelativePath = null;

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

            if (managedSimpleName is not null)
            {
                managedAssemblyRelativePaths.Add(relativePath);
                if (IsFrameworkSimpleName(managedSimpleName))
                    frameworkRelativePath = relativePath;
            }

            copyEntries.Add(new GenerationCopyEntry(sourceFile, relativePath));
            contentRelativePaths.Add(relativePath);
        }

        if (frameworkRelativePath is null)
        {
            throw new NUnitGenerationBuildException(
                $"Exactly one {NUnitGenerationBuilder.FrameworkAssemblyFileName} is required in the test output directory.");
        }

        AppendRuntime(runtimeSource, copyEntries, contentRelativePaths, managedAssemblyRelativePaths);

        contentRelativePaths.Sort(StringComparer.OrdinalIgnoreCase);
        managedAssemblyRelativePaths.Sort(StringComparer.OrdinalIgnoreCase);

        return new GenerationCopyPlan(
            sourceAssemblyRelativePath,
            frameworkRelativePath,
            copyEntries,
            contentRelativePaths,
            managedAssemblyRelativePaths);
    }

    private static bool TryIncludeOutputFile(
        string sourceFile,
        string sourceOutputDirectory,
        out string relativePath,
        out string? managedSimpleName)
    {
        relativePath = NUnitGenerationPaths.NormalizeRelativePath(
            NUnitGenerationPaths.GetRelativePath(sourceOutputDirectory, sourceFile));
        managedSimpleName = null;

        if (NUnitGenerationPaths.IsVolatileGenerationOutput(relativePath))
            return false;

        if (NUnitSharedAssemblyPolicy.ShouldExcludeFromGenerationCopy(sourceFile))
            return false;

        if (NUnitSharedAssemblyPolicy.TryGetManagedAssemblyIdentity(sourceFile, out var simpleName))
            managedSimpleName = simpleName;

        return true;
    }

    private static void AppendRuntime(
        NUnitRuntimeSource runtimeSource,
        List<GenerationCopyEntry> copyEntries,
        List<string> contentRelativePaths,
        List<string> managedAssemblyRelativePaths)
    {
        copyEntries.Add(new GenerationCopyEntry(
            runtimeSource.AssemblyPath,
            NUnitGenerationBuilder.RuntimeAssemblyFileName));
        contentRelativePaths.Add(NUnitGenerationBuilder.RuntimeAssemblyFileName);
        managedAssemblyRelativePaths.Add(NUnitGenerationBuilder.RuntimeAssemblyFileName);

        if (!string.IsNullOrWhiteSpace(runtimeSource.SymbolPath))
        {
            copyEntries.Add(new GenerationCopyEntry(
                runtimeSource.SymbolPath!,
                NUnitGenerationBuilder.RuntimeSymbolFileName));
            contentRelativePaths.Add(NUnitGenerationBuilder.RuntimeSymbolFileName);
        }

        foreach (var dependencyPath in runtimeSource.DependencyPaths)
            MergeRuntimeDependency(dependencyPath, copyEntries, contentRelativePaths, managedAssemblyRelativePaths);
    }

    private static void MergeRuntimeDependency(
        string dependencyPath,
        List<GenerationCopyEntry> copyEntries,
        List<string> contentRelativePaths,
        List<string> managedAssemblyRelativePaths)
    {
        if (NUnitSharedAssemblyPolicy.ShouldExcludeFromGenerationCopy(dependencyPath))
            return;

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
        contentRelativePaths.Add(relativePath);
        if (NUnitSharedAssemblyPolicy.TryGetManagedAssemblyIdentity(dependencyPath, out _))
            managedAssemblyRelativePaths.Add(relativePath);
    }

    private static bool IsRuntimeOwnedFileName(string relativePath) =>
        string.Equals(relativePath, NUnitGenerationBuilder.RuntimeAssemblyFileName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(relativePath, NUnitGenerationBuilder.RuntimeSymbolFileName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(relativePath, NUnitGenerationBuilder.FrameworkAssemblyFileName, StringComparison.OrdinalIgnoreCase);

    private static bool IsFrameworkSimpleName(string simpleName) =>
        string.Equals(
            simpleName,
            Path.GetFileNameWithoutExtension(NUnitGenerationBuilder.FrameworkAssemblyFileName),
            StringComparison.OrdinalIgnoreCase);

    private static void ValidateNUnitFramework(IReadOnlyList<string> outputFiles, string sourceOutputDirectory)
    {
        var frameworkMatches = outputFiles
            .Where(path => string.Equals(
                Path.GetFileName(path),
                NUnitGenerationBuilder.FrameworkAssemblyFileName,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (frameworkMatches.Count == 0)
        {
            throw new NUnitGenerationBuildException(
                $"Exactly one {NUnitGenerationBuilder.FrameworkAssemblyFileName} {NUnitGenerationBuilder.ExpectedNUnitPackageVersion} is required; none was found.");
        }

        if (frameworkMatches.Count > 1)
        {
            throw new NUnitGenerationBuildException(
                $"Exactly one {NUnitGenerationBuilder.FrameworkAssemblyFileName} {NUnitGenerationBuilder.ExpectedNUnitPackageVersion} is required; found {frameworkMatches.Count}.");
        }

        NUnitGenerationBuilder.ValidateNUnitFrameworkVersion(frameworkMatches[0], sourceOutputDirectory);
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
