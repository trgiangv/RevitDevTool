using Microsoft.Extensions.Logging;
using Serilog.Events;
using SerilogTimeInterval = Serilog.RollingInterval;
using InternalTimeInterval = RevitDevTool.Models.Config.RollingInterval;
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
    
    public static SerilogTimeInterval ToSerilog(this InternalTimeInterval interval)
    {
        return interval switch
        {
            InternalTimeInterval.Infinite => SerilogTimeInterval.Infinite,
            InternalTimeInterval.Year => SerilogTimeInterval.Year,
            InternalTimeInterval.Month => SerilogTimeInterval.Month,
            InternalTimeInterval.Day => SerilogTimeInterval.Day,
            InternalTimeInterval.Hour => SerilogTimeInterval.Hour,
            InternalTimeInterval.Minute => SerilogTimeInterval.Minute,
            _ => SerilogTimeInterval.Day
        };
    }
}