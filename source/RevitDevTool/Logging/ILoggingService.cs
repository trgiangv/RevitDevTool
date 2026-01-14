using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace RevitDevTool.Logging;

/// <summary>
/// Central service for managing application logging lifecycle.
/// Coordinates logger creation, trace listener registration, and UI output.
/// </summary>
public interface ILoggingService : IDisposable
{
    ILoggerAdapter Logger { get; }
    ILogOutputSink? OutputSink { get; }
    TraceListener? TraceListener { get; }

    void Initialize(bool isDarkTheme);
    void Restart(bool isDarkTheme);
    void SetMinimumLevel(LogLevel level);
    void RegisterTraceListeners();
    void UnregisterTraceListeners();
    void ClearOutput();
}

