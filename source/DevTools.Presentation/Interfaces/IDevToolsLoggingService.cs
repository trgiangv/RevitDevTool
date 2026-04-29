using System.Windows;
using DevTools.Logging.Options;
using Microsoft.Extensions.Logging;

namespace DevTools.Presentation.Interfaces;

public interface IDevToolsLoggingService : IDisposable
{
    FrameworkElement? HostElement { get; }
    void Initialize();
    void EnableTarget(LogSink sink);
    void SetMinimumLevel(LogLevel level);
    void SetPrettyJson(bool enabled);
    void SetTheme(bool isDark);
    void RegisterTraceListeners();
    void UnregisterTraceListeners();
    void ClearOutput();
}
