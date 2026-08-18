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
        var sourceAssemblyRelativePath = NormalizeRelativePath(
            Path.GetRelativePath(sourceOutputDirectory, sourceAssemblyPath));

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
                $"Exactly one {NUnitGenerationPolicy.FrameworkAssemblyFileName} is required in the test output directory.");
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
        relativePath = NormalizeRelativePath(
            Path.GetRelativePath(sourceOutputDirectory, sourceFile));
        managedSimpleName = null;

        if (IsVolatileGenerationOutput(relativePath))
            return false;

        if (TryGetManagedAssemblyIdentity(sourceFile, out var simpleName))
        {
            // The runtime session binds this concrete contract identity from the
            // parent (host) load context. Never copy it from an arbitrary
            // generation file, including a renamed copy.
            if (string.Equals(
                    simpleName,
                    typeof(DevTools.Testing.Abstractions.Runtime.ITestingRuntimeSession).Assembly.GetName().Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            managedSimpleName = simpleName;
        }

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
            NUnitGenerationPolicy.RuntimeAssemblyFileName));
        contentRelativePaths.Add(NUnitGenerationPolicy.RuntimeAssemblyFileName);
        managedAssemblyRelativePaths.Add(NUnitGenerationPolicy.RuntimeAssemblyFileName);

        if (!string.IsNullOrWhiteSpace(runtimeSource.SymbolPath))
        {
            copyEntries.Add(new GenerationCopyEntry(
                runtimeSource.SymbolPath!,
                NUnitGenerationPolicy.RuntimeSymbolFileName));
            contentRelativePaths.Add(NUnitGenerationPolicy.RuntimeSymbolFileName);
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
        if (TryGetManagedAssemblyIdentity(dependencyPath, out _))
            managedAssemblyRelativePaths.Add(relativePath);
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

    private static bool TryGetManagedAssemblyIdentity(string filePath, out string? simpleName)
    {
        simpleName = null;
        if (!Path.GetExtension(filePath).Equals(".dll", StringComparison.OrdinalIgnoreCase)
            && !Path.GetExtension(filePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            simpleName = System.Reflection.AssemblyName.GetAssemblyName(filePath).Name;
            return !string.IsNullOrWhiteSpace(simpleName);
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (FileLoadException)
        {
            return false;
        }
    }

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

    internal static string NormalizeRelativePath(string relativePath) => relativePath.Replace('/', '\\');

    private static bool IsVolatileGenerationOutput(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        var root = normalized.Split('\\')[0];
        if (root.Equals("Log", StringComparison.OrdinalIgnoreCase)
            || root.Equals("TestResults", StringComparison.OrdinalIgnoreCase))
            return true;

        var extension = Path.GetExtension(normalized);
        return extension.Equals(".diag", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".log", StringComparison.OrdinalIgnoreCase);
    }
}
