using InternalTimeInterval = RevitDevTool.Models.Config.RollingInterval;
using ZloggerTimeInterval = ZLogger.Providers.RollingInterval;

namespace RevitDevTool.Logging.ZLogger;

public static class ZloggerUtils
{
    public static ZloggerTimeInterval ToZlogger(this InternalTimeInterval interval)
    {
        return interval switch
        {
            InternalTimeInterval.Infinite => ZloggerTimeInterval.Infinite,
            InternalTimeInterval.Year => ZloggerTimeInterval.Year,
            InternalTimeInterval.Month => ZloggerTimeInterval.Month,
            InternalTimeInterval.Day => ZloggerTimeInterval.Day,
            InternalTimeInterval.Hour => ZloggerTimeInterval.Hour,
            InternalTimeInterval.Minute => ZloggerTimeInterval.Minute,
            _ => ZloggerTimeInterval.Day
        };
    }
}