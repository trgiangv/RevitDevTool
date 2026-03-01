using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Search;

namespace RevitDevTool.Scintilla.Control;

public sealed class LogViewerControlEvents : ILogViewerControlEvents
{
    public event Action? StartRequested;
    public event Action? StopRequested;
    public event Action<ClearMode>? ClearRequested;
    public event Action<bool>? AutoScrollChanged;
    public event Action<LogFilterOptions>? FilterRequested;
    public event Action<LogSearchRequest>? SearchRequested;
    public event Action<bool>? RenderModeChanged;
    public event Action<ScintillaTheme>? ThemeChanged;

    public void RequestStart() => StartRequested?.Invoke();
    public void RequestStop() => StopRequested?.Invoke();
    public void RequestClear(ClearMode mode) => ClearRequested?.Invoke(mode);
    public void RequestSetAutoScroll(bool enabled) => AutoScrollChanged?.Invoke(enabled);
    public void RequestFilter(LogFilterOptions options) => FilterRequested?.Invoke(options);
    public void RequestSearch(LogSearchRequest request) => SearchRequested?.Invoke(request);
    public void RequestRenderMode(bool enablePrettyJson) => RenderModeChanged?.Invoke(enablePrettyJson);
    public void RequestTheme(ScintillaTheme theme) => ThemeChanged?.Invoke(theme);
}
