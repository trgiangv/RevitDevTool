using Serilog.Events;
namespace RevitDevTool.Logging;

/// <summary>
/// Central service for managing application logging lifecycle.
/// Coordinates logger creation, trace listener registration, and UI output.
/// </summary>
public interface ILoggingService : IDisposable
{
    ILoggingMonitor? Monitor { get; }
    void Initialize();
    void Restart();
    void SetMinimumLevel(LogEventLevel level);
    void RegisterTraceListeners();
    void UnregisterTraceListeners();
    void ClearOutput();
}
