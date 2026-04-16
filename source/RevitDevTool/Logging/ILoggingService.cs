using System.Windows;
using DevTools.Logging.Options;
using Microsoft.Extensions.Logging;

namespace RevitDevTool.Logging;

public interface ILoggingService : IDisposable
{
    FrameworkElement? HostElement { get; }
    void Initialize();
    void EnableTarget(LogSink sink);
    void DisableTarget(LogSink sink);
    void SetMinimumLevel(LogLevel level);
    void SetPrettyJson(bool enabled);
    void SetTheme(bool isDark);
    void RegisterTraceListeners();
    void UnregisterTraceListeners();
    void ClearOutput();
}
