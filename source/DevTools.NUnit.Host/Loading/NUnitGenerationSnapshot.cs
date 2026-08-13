namespace DevTools.NUnit.Host.Loading;

internal static class NUnitGenerationSnapshot
{
    internal static bool TryCopy(
        string stagingDirectory,
        GenerationCopyPlan plan,
        Action<string, SnapshotCopyPhase>? progress)
    {
        Directory.CreateDirectory(stagingDirectory);

        var sourceMetadata = plan.CopyEntries
            .Select(entry => (Entry: entry, Metadata: CaptureSourceMetadata(entry.SourcePath)))
            .ToList();

        foreach (var item in sourceMetadata)
        {
            progress?.Invoke(item.Entry.SourcePath, SnapshotCopyPhase.BeforeCopy);

            var destinationPath = Path.Combine(stagingDirectory, item.Entry.RelativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            CopyWithoutLockingSource(item.Entry.SourcePath, destinationPath);

            progress?.Invoke(item.Entry.SourcePath, SnapshotCopyPhase.AfterCopy);
        }

        return sourceMetadata.TrueForAll(item =>
            SourceMetadataMatches(item.Entry.SourcePath, item.Metadata));
    }

    internal static bool ValidateStaged(string stagingDirectory, GenerationCopyPlan plan)
    {
        var stagedTestAssembly = Path.Combine(stagingDirectory, plan.SourceAssemblyRelativePath);
        if (!NUnitSharedAssemblyPolicy.TryGetManagedAssemblyIdentity(stagedTestAssembly, out _))
            return false;

        var stagedFramework = Path.Combine(stagingDirectory, plan.FrameworkRelativePath);
        try
        {
            NUnitGenerationBuilder.ValidateNUnitFrameworkVersion(stagedFramework);
        }
        catch (NUnitGenerationBuildException)
        {
            return false;
        }

        foreach (var relativePath in plan.ManagedAssemblyRelativePaths)
        {
            var stagedPath = Path.Combine(stagingDirectory, relativePath);
            if (!NUnitSharedAssemblyPolicy.TryGetManagedAssemblyIdentity(stagedPath, out _))
                return false;
        }

        return true;
    }

    internal static string ComputeGenerationId(
        string snapshotDirectory,
        IReadOnlyList<string> contentRelativePaths)
    {
        var entries = contentRelativePaths
            .Select(relativePath => (
                RelativePath: relativePath,
                AbsolutePath: Path.Combine(snapshotDirectory, relativePath)))
            .ToList();

        return NUnitGenerationContentHash.ComputeGenerationId(entries);
    }

    internal static bool MatchesGenerationId(
        string snapshotDirectory,
        IReadOnlyList<string> contentRelativePaths,
        string expectedGenerationId) =>
        string.Equals(
            ComputeGenerationId(snapshotDirectory, contentRelativePaths),
            expectedGenerationId,
            StringComparison.Ordinal);

    internal static void Publish(string stagingDirectory, string shadowDirectory, string generationId)
    {
        File.WriteAllText(
            Path.Combine(stagingDirectory, NUnitGenerationBuilder.GenerationCompleteMarkerFileName),
            string.Empty);

        if (!MatchesGenerationId(
                stagingDirectory,
                ReadContentRelativePaths(stagingDirectory),
                generationId))
        {
            throw new NUnitGenerationBuildException(
                "Refusing to publish a generation whose snapshot no longer matches its generation ID.");
        }

        if (Directory.Exists(shadowDirectory))
            return;

        try
        {
            Directory.Move(stagingDirectory, shadowDirectory);
        }
        catch (IOException) when (Directory.Exists(shadowDirectory))
        {
            // Another process published the same generation first.
        }
    }

    internal static IReadOnlyList<string> ReadContentRelativePaths(string snapshotDirectory) =>
        Directory.EnumerateFiles(snapshotDirectory, "*", SearchOption.AllDirectories)
            .Select(path => NUnitGenerationPaths.NormalizeRelativePath(
                NUnitGenerationPaths.GetRelativePath(snapshotDirectory, path)))
            .Where(relativePath => !string.Equals(
                relativePath,
                NUnitGenerationBuilder.GenerationCompleteMarkerFileName,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal static bool IsPublished(string shadowDirectory) =>
        Directory.Exists(shadowDirectory)
        && File.Exists(Path.Combine(shadowDirectory, NUnitGenerationBuilder.GenerationCompleteMarkerFileName));

    internal static void EnsurePublishedIsValid(string shadowDirectory, string expectedGenerationId)
    {
        if (!IsPublished(shadowDirectory))
        {
            throw new NUnitGenerationBuildException(
                $"Expected published generation at '{shadowDirectory}' but the completion marker is missing.");
        }

        var actualGenerationId = ComputeGenerationId(
            shadowDirectory,
            ReadContentRelativePaths(shadowDirectory));

        if (!string.Equals(actualGenerationId, expectedGenerationId, StringComparison.Ordinal))
        {
            throw new NUnitGenerationCorruptionException(
                shadowDirectory,
                expectedGenerationId,
                actualGenerationId);
        }
    }

    private static SourceFileMetadata CaptureSourceMetadata(string sourcePath)
    {
        var info = new FileInfo(sourcePath);
        return new SourceFileMetadata(info.Length, info.LastWriteTimeUtc);
    }

    private static bool SourceMetadataMatches(string sourcePath, SourceFileMetadata metadata)
    {
        var info = new FileInfo(sourcePath);
        return info.Length == metadata.Length
            && info.LastWriteTimeUtc == metadata.LastWriteUtc;
    }

    private static void CopyWithoutLockingSource(string sourcePath, string destinationPath)
    {
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

    private sealed record SourceFileMetadata(long Length, DateTime LastWriteUtc);
}
