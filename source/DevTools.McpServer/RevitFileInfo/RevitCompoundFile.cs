using OpenMcdf;

namespace DevTools.McpServer.RevitFileInfo;

internal sealed class RevitCompoundFile : IDisposable
{
    private readonly RootStorage _storage;

    private RevitCompoundFile(RootStorage storage) => _storage = storage;

    public static RevitCompoundFile Open(string filePath) => new(RootStorage.OpenRead(filePath));

    /// <summary>
    /// Reads a root-level OLE stream into a <see cref="MemoryStream"/>.
    /// Returns null if the stream does not exist.
    /// </summary>
    public MemoryStream? TryReadStream(string streamName) =>
        CopyStream(_storage, streamName);

    /// <summary>
    /// Reads an OLE stream inside a sub-storage (e.g. "Global" / "PartitionTable").
    /// Returns null if the storage or stream does not exist.
    /// </summary>
    public MemoryStream? TryReadStream(string storageName, string streamName)
    {
        Storage sub;
        try
        {
            sub = _storage.OpenStorage(storageName);
        }
        catch
        {
            return null;
        }

        return CopyStream(sub, streamName);
    }

    private static MemoryStream? CopyStream(Storage storage, string streamName)
    {
        CfbStream stream;
        try
        {
            stream = storage.OpenStream(streamName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or InvalidOperationException or FormatException
                                       or FileNotFoundException)
        {
            return null;
        }

        using (stream)
        {
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }
    }

    public void Dispose() => _storage.Dispose();
}
