namespace DevTools.Daemon.Desktop;

internal static class AppConstants
{
    public const string StdioArg = "--stdio";
    public const string StartupErrorTitle = "DevTools Daemon \u2014 Startup Error";
    public const string MutexName = "DevToolsDaemon_v1";
    public const string AutoStartValueName = "DevToolsDaemon";
    public const int ShutdownTimeoutSeconds = 5;
}
