using RevitDevTool.Models.Config;
using System.Diagnostics;

namespace RevitDevTool.Logging.Serilog;

/// <summary>
/// Factory for creating Serilog-based TraceListeners.
/// </summary>
[UsedImplicitly]
internal sealed class SerilogTraceListenerFactory : ITraceListenerFactory
{
    public TraceListener CreateTraceListener(ILoggerAdapter logger, LogConfig config)
    {
        return logger is not SerilogAdapter
            ? throw new ArgumentException("Logger must be a SerilogAdapter for SerilogTraceListenerFactory", nameof(logger))
            : new AdapterTraceListener(logger, config.IncludeStackTrace, config.StackTraceDepth, config.FilterKeywords);
    }
}

