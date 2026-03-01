using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Render;

public interface ISegmentLogRenderStrategy : ILogRenderStrategy
{
    void BuildSegments(LogEntry entry, IList<RenderSegment> segments);
    bool TryFormatLine(LogEntry entry, out byte[] formattedLine);
}
