using System.Windows;
using Microsoft.Extensions.Logging;
namespace DevTools.Logging.Abstractions;

public interface IMonitorLogTarget : IDisposable
{
    FrameworkElement HostElement { get; }
    void SetFilter(LogLevel level);
    void SetTheme(bool isDark);
    void SetPrettyJson(bool enabled);
    void Clear();
}
