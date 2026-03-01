using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Search;
namespace RevitDevTool.Scintilla.Control;

public interface ILogViewerControlEvents
{
    event Action? StartRequested;
    event Action? StopRequested;
    event Action<ClearMode>? ClearRequested;
    event Action<bool>? AutoScrollChanged;
    event Action<LogFilterOptions>? FilterRequested;
    event Action<LogSearchRequest>? SearchRequested;
    event Action<bool>? RenderModeChanged;
    event Action<ScintillaTheme>? ThemeChanged;

    void RequestStart();
    void RequestStop();
    void RequestClear(ClearMode mode);
    void RequestSetAutoScroll(bool enabled);
    void RequestFilter(LogFilterOptions options);
    void RequestSearch(LogSearchRequest request);
    void RequestRenderMode(bool enablePrettyJson);
    void RequestTheme(ScintillaTheme theme);
}
