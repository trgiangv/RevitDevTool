using RevitDevTool.Listeners;
using RevitDevTool.Settings.Config;

namespace RevitDevTool.Logging;

/// <summary>
/// Factory for creating TraceListener instances configured for logging.
/// </summary>
public interface ITraceListenerFactory
{
    LoggerTraceListener CreateTraceListener(ILoggerAdapter logger, LogConfig config);
}

