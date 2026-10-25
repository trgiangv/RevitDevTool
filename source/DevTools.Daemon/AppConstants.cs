namespace DevTools.Daemon;

internal static class AppConstants
{
    public const string StdioArg = "--stdio";
    public const string SelectArg = "--select";
    public const string SourceArg = "--source";
    public const string NotifyArg = "--notify";
    public const string StartupErrorTitle = "DevTools Daemon \u2014 Startup Error";
    public const string MutexName = "DevToolsDaemon_v1";
    public const string AutoStartValueName = "DevToolsDaemon";
    public const int ShutdownTimeoutSeconds = 5;
}
