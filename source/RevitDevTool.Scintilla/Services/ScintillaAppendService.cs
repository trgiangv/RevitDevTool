using System.Buffers;
using System.Runtime.InteropServices;
namespace RevitDevTool.Scintilla.Services;

internal sealed class ScintillaAppendService : IDisposable
{
    private readonly ScintillaNET.Scintilla _scintilla;
    private byte[] _buffer;

    public ScintillaAppendService(ScintillaNET.Scintilla scintilla, int initialSize = 128 * 1024)
    {
        _scintilla = scintilla;
        _buffer = ArrayPool<byte>.Shared.Rent(initialSize);
    }

    public byte[] Buffer => _buffer;

    public void EnsureCapacity(int requiredSize, int bytesUsed)
    {
        if (requiredSize <= _buffer.Length)
            return;

        var newSize = _buffer.Length;
        while (newSize < requiredSize)
            newSize *= 2;

        var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        System.Buffer.BlockCopy(_buffer, 0, newBuffer, 0, bytesUsed);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }

    public void Append(int count, int sciAppendTextMessageId)
    {
        if (count <= 0)
            return;

        var handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        try
        {
            var pointer = handle.AddrOfPinnedObject();
            _scintilla.DirectMessage(sciAppendTextMessageId, (IntPtr)count, pointer);
        }
        finally
        {
            handle.Free();
        }
    }

    public void ResetLargeBuffer(int maxRetainedSize = 256 * 1024, int resetSize = 128 * 1024)
    {
        if (_buffer.Length <= maxRetainedSize)
            return;

        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = ArrayPool<byte>.Shared.Rent(resetSize);
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = Array.Empty<byte>();
    }
}
