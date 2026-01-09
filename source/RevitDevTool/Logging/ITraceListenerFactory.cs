using System.Diagnostics;
using RevitDevTool.Models.Config;

namespace RevitDevTool.Logging;

/// <summary>
/// Factory for creating TraceListener instances configured for logging.
/// </summary>
public interface ITraceListenerFactory
{
    TraceListener CreateTraceListener(ILoggerAdapter logger, LogConfig config);
}

