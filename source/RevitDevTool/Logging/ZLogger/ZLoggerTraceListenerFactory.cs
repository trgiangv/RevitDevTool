using RevitDevTool.Models.Config;
using System.Diagnostics;

namespace RevitDevTool.Logging.ZLogger;

/// <summary>
/// Factory for creating ZLogger-based TraceListeners.
/// </summary>
[UsedImplicitly]
internal sealed class ZLoggerTraceListenerFactory : ITraceListenerFactory
{
    public TraceListener CreateTraceListener(ILoggerAdapter logger, LogConfig config)
    {
        return logger is not ZLoggerAdapter
            ? throw new ArgumentException("Logger must be a ZLoggerAdapter for ZLoggerTraceListenerFactory", nameof(logger))
            : new AdapterTraceListener(logger, config.IncludeStackTrace, config.StackTraceDepth);
    }
}
