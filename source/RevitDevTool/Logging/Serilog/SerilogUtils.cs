using Microsoft.Extensions.Logging;
using Serilog.Events;
namespace RevitDevTool.Logging.Serilog;

public static class SerilogUtils
{
    public static LogEventLevel ToSerilog(this LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Debug
        };
    }
}