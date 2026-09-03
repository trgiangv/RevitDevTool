using Microsoft.Win32;

namespace DevTools.Daemon.Desktop;

public static class AutoStart
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(AppConstants.AutoStartValueName) is not null;
        }
    }

    public static void Enable()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is null) return;

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.SetValue(AppConstants.AutoStartValueName, $"\"{exePath}\"");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(AppConstants.AutoStartValueName, throwOnMissingValue: false);
    }
}
