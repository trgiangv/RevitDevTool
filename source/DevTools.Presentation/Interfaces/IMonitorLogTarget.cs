using DevTools.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace DevTools.Presentation.Interfaces;

public interface IMonitorLogTarget : IDisposable, IActivatable
{
    FrameworkElement HostElement { get; }
    void SetFilter(LogLevel level);
    void SetTheme(bool isDark);
    void SetPrettyJson(bool enabled);
    void Clear();
}
