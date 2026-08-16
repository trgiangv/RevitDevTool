using System.Windows;
using DevTools.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace DevTools.Presentation.Interfaces;

public interface IMonitorLogTarget : IDisposable, IActivatable
{
    FrameworkElement HostElement { get; }
    void SetFilter(LogLevel level);
    void SetTheme(bool isDark);
    void SetPrettyJson(bool enabled);
    void Clear();
}
