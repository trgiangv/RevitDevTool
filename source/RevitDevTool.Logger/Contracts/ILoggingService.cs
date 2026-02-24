using Microsoft.Extensions.Logging;
namespace RevitDevTool.Logger.Contracts;

/// <summary>
/// Central service for managing application logging lifecycle.
/// Coordinates logger creation, trace listener registration, and UI output.
/// </summary>
public interface ILoggingService : IDisposable
{
    ILogOutputSink? OutputSink { get; }
    void Initialize();
    void Restart();
    void SetMinimumLevel(LogLevel level);
    void RegisterTraceListeners();
    void UnregisterTraceListeners();
    void ClearOutput();
}
