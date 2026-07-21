namespace DevTools.Daemon;

/// <summary>
/// Tray app UI and process lifecycle constants.
/// </summary>
public static class TrayUiConstants
{
    public const string TrayIconResourceKey = "TrayIcon";
    public const string StdioArg = "--stdio";
    public const string StartupErrorTitle = "DevTools Daemon \u2014 Startup Error";
    public const int ShutdownTimeoutSeconds = 5;
}
