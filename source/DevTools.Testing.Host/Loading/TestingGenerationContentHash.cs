using System.Security.Cryptography;
using System.Text;

namespace DevTools.Testing.Host.Loading;

internal static class TestingGenerationContentHash
{
    private const byte FormatVersion = 1;

    internal static string ComputeGenerationId(IEnumerable<(string RelativePath, string AbsolutePath)> entries)
    {
        var orderedEntries = entries
            .Select(entry => (
                CanonicalPath: CanonicalizeRelativePath(entry.RelativePath),
                entry.AbsolutePath))
            .OrderBy(static entry => entry.CanonicalPath, StringComparer.Ordinal)
            .ToList();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(new[] { FormatVersion });

        foreach (var entry in orderedEntries)
            AppendEntry(hash, entry.CanonicalPath, entry.AbsolutePath);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendEntry(IncrementalHash hash, string canonicalPath, string absolutePath)
    {
        var pathBytes = Encoding.UTF8.GetBytes(canonicalPath);
        AppendUInt32LittleEndian(hash, checked((uint)pathBytes.Length));
        hash.AppendData(pathBytes);

        using var stream = File.OpenRead(absolutePath);
        AppendInt64LittleEndian(hash, stream.Length);

        var buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            hash.AppendData(buffer, 0, read);
    }

    private static string CanonicalizeRelativePath(string relativePath) =>
        TestingGenerationPaths.NormalizeRelativePath(relativePath).ToLowerInvariant();

    private static void AppendUInt32LittleEndian(IncrementalHash hash, uint value)
    {
        hash.AppendData(new byte[]
        {
            (byte)value,
            (byte)(value >> 8),
            (byte)(value >> 16),
            (byte)(value >> 24),
        });
    }

    private static void AppendInt64LittleEndian(IncrementalHash hash, long value)
    {
        hash.AppendData(new byte[]
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
