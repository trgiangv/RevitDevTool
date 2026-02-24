using Microsoft.Extensions.Logging;
using RevitDevTool.Logger.Enums;
using Serilog.Events;
using SerilogRollingInterval = Serilog.RollingInterval;

namespace RevitDevTool.Logger.Serilog;

public static class SerilogExtensions
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

    public static SerilogRollingInterval ToSerilog(this RollingInterval interval)
    {
        return interval switch
        {
            RollingInterval.Infinite => SerilogRollingInterval.Infinite,
            RollingInterval.Year => SerilogRollingInterval.Year,
            RollingInterval.Month => SerilogRollingInterval.Month,
            RollingInterval.Day => SerilogRollingInterval.Day,
            RollingInterval.Hour => SerilogRollingInterval.Hour,
            RollingInterval.Minute => SerilogRollingInterval.Minute,
            _ => SerilogRollingInterval.Day
        };
    }
}
