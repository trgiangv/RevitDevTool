using RevitDevTool.Logger.Config;
using RevitDevTool.Logger.Listeners;
namespace RevitDevTool.Logger.Contracts;

/// <summary>
/// Factory for creating TraceListener instances configured for logging.
/// </summary>
public interface ITraceListenerFactory
{
    LoggerTraceListener CreateTraceListener(ILoggerAdapter logger, LogConfigCore config);
}
