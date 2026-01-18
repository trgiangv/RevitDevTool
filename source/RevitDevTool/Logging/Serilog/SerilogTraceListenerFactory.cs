using RevitDevTool.Listeners;
using RevitDevTool.Settings.Config;

namespace RevitDevTool.Logging.Serilog;

/// <summary>
/// Factory for creating Serilog-based TraceListeners.
/// </summary>
[UsedImplicitly]
internal sealed class SerilogTraceListenerFactory : ITraceListenerFactory
{
    public LoggerTraceListener CreateTraceListener(ILoggerAdapter logger, LogConfig config)
    {
        return logger is not SerilogAdapter
            ? throw new ArgumentException("Logger must be a SerilogAdapter for SerilogTraceListenerFactory", nameof(logger))
            : new LoggerTraceListener(logger, config);
    }
}

