using System.Security.Cryptography;
using System.Text;

namespace DevTools.Testing.Host.Loading;

internal static class TestingGenerationPublish
{
    private const byte FormatVersion = 1;
    internal const string CompleteMarkerFileName = ".generation-complete";

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

        return ComputeGenerationId(entries);
    }

    internal static string ComputeGenerationId(IEnumerable<(string RelativePath, string AbsolutePath)> entries)
    {
        var orderedEntries = entries
            .Select(entry => (
                CanonicalPath: entry.RelativePath.ToLowerInvariant(),
                entry.AbsolutePath))
            .OrderBy(static entry => entry.CanonicalPath, StringComparer.Ordinal)
            .ToList();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData([FormatVersion]);

        foreach (var entry in orderedEntries)
            AppendEntry(hash, entry.CanonicalPath, entry.AbsolutePath);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    internal static IReadOnlyList<string> ReadContentRelativePaths(string snapshotDirectory) =>
        Directory.EnumerateFiles(snapshotDirectory, "*", SearchOption.AllDirectories)
            .Select(path => TestingGenerationFiles.GetRelativePath(snapshotDirectory, path))
            .Where(relativePath => !string.Equals(
                relativePath,
                CompleteMarkerFileName,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal static void Publish(string stagingDirectory, string shadowDirectory, string generationId)
    {
        File.WriteAllText(
            Path.Combine(stagingDirectory, CompleteMarkerFileName),
            string.Empty);

        var actual = ComputeGenerationId(stagingDirectory, ReadContentRelativePaths(stagingDirectory));
        if (!string.Equals(actual, generationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Refusing to publish a generation whose staged files no longer match its generation ID.");
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
            var relative = TestingGenerationFiles.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = TestingGenerationFiles.GetRelativePath(sourceDirectory, file);
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
            || !File.Exists(Path.Combine(shadowDirectory, CompleteMarkerFileName)))
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

    private static void AppendEntry(IncrementalHash hash, string canonicalPath, string absolutePath)
    {
        var pathBytes = Encoding.UTF8.GetBytes(canonicalPath);
        AppendUInt32LittleEndian(hash, checked((uint)pathBytes.Length));
        hash.AppendData(pathBytes);

        using var stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        AppendInt64LittleEndian(hash, stream.Length);

        var buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            hash.AppendData(buffer, 0, read);
    }

    private static void AppendUInt32LittleEndian(IncrementalHash hash, uint value)
    {
        hash.AppendData(new[]
        {
            (byte)value,
            (byte)(value >> 8),
            (byte)(value >> 16),
            (byte)(value >> 24),
        });
    }

    private static void AppendInt64LittleEndian(IncrementalHash hash, long value)
    {
        hash.AppendData(new[]
        {
            (byte)value,
            (byte)(value >> 8),
            (byte)(value >> 16),
            (byte)(value >> 24),
            (byte)(value >> 32),
            (byte)(value >> 40),
            (byte)(value >> 48),
            (byte)(value >> 56),
        });
    }
}
