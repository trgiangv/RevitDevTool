namespace DevTools.Testing.Host.Loading;

internal static class TestingGenerationSnapshot
{
    internal static void CopyFile(string sourcePath, string destinationPath)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);

        source.CopyTo(destination);
    }

    internal static string ComputeGenerationId(string snapshotDirectory, IReadOnlyList<string> contentRelativePaths)
    {
        var entries = contentRelativePaths
            .Select(relativePath => (
                RelativePath: relativePath,
                AbsolutePath: Path.Combine(snapshotDirectory, relativePath)))
            .ToList();

        return TestingGenerationContentHash.ComputeGenerationId(entries);
    }

    internal static IReadOnlyList<string> ReadContentRelativePaths(string snapshotDirectory) =>
        Directory.EnumerateFiles(snapshotDirectory, "*", SearchOption.AllDirectories)
            .Select(path => TestingGenerationPaths.NormalizeRelativePath(
                TestingGenerationPaths.GetRelativePath(snapshotDirectory, path)))
            .Where(relativePath => !string.Equals(
                relativePath,
                TestingGenerationPaths.GenerationCompleteMarkerFileName,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal static void Publish(string stagingDirectory, string shadowDirectory, string generationId)
    {
        File.WriteAllText(
            Path.Combine(stagingDirectory, TestingGenerationPaths.GenerationCompleteMarkerFileName),
            string.Empty);

        var actual = ComputeGenerationId(stagingDirectory, ReadContentRelativePaths(stagingDirectory));
        if (!string.Equals(actual, generationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Refusing to publish a generation whose snapshot no longer matches its generation ID.");
        }

        if (Directory.Exists(shadowDirectory))
            return;

        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                Directory.Move(stagingDirectory, shadowDirectory);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (Directory.Exists(shadowDirectory))
                    return;

                if (attempt < 3)
                {
                    Thread.Sleep(25 * (attempt + 1));
                    continue;
                }

                try
                {
                    CopyDirectory(stagingDirectory, shadowDirectory);
                    TryDeleteDirectory(stagingDirectory);
                    return;
                }
                catch (Exception)
                {
                    if (Directory.Exists(shadowDirectory))
                        TryDeleteDirectory(shadowDirectory);

                    throw;
                }
            }
        }
    }

    internal static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = TestingGenerationPaths.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = TestingGenerationPaths.GetRelativePath(sourceDirectory, file);
            CopyFile(file, Path.Combine(destinationDirectory, relative));
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception)
        {
            // Best effort; a leftover staging tree is harmless once the shadow exists,
            // and a leftover partial shadow must not look published on the next Build.
        }
    }

    internal static void EnsurePublishedIsValid(string shadowDirectory, string expectedGenerationId)
    {
        if (!Directory.Exists(shadowDirectory)
            || !File.Exists(Path.Combine(shadowDirectory, TestingGenerationPaths.GenerationCompleteMarkerFileName)))
        {
            throw new TestingGenerationBuildException(
                $"Expected a complete published generation at '{shadowDirectory}'.");
        }

        var actualGenerationId = ComputeGenerationId(
            shadowDirectory,
            ReadContentRelativePaths(shadowDirectory));
        if (!string.Equals(actualGenerationId, expectedGenerationId, StringComparison.Ordinal))
        {
            throw new TestingGenerationCorruptionException(
                shadowDirectory,
                expectedGenerationId,
                actualGenerationId);
        }
    }
}
