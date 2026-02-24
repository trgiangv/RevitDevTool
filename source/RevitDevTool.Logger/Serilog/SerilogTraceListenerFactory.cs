using RevitDevTool.Logger.Config;
using RevitDevTool.Logger.Contracts;
namespace RevitDevTool.Logger.Serilog;

/// <summary>
/// Factory for creating Serilog-based TraceListeners.
/// </summary>
public sealed class SerilogTraceListenerFactory : ITraceListenerFactory
{
    public Listeners.LoggerTraceListener CreateTraceListener(ILoggerAdapter logger, LogConfigCore config)
    {
        return new Listeners.LoggerTraceListener(logger, config);
    }
}
