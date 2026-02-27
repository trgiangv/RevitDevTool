#if NET8_0_OR_GREATER
namespace RevitDevTool.Scintilla.Contracts;

public interface IUtf8LogRenderStrategy : ILogRenderStrategy
{
    int GetLineUtf8ByteCount(LogEntry entry);
    int WriteLineUtf8(LogEntry entry, Span<byte> destination);
}
#endif
