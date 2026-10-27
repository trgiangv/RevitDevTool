using DevTools.Ipc;
using Microsoft.Win32;
using System.IO;

namespace DevTools.Daemon.Control;

public static class StartupRegistration
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

    /// <summary>
    ///     The Velopack stub beside <c>current\</c> when installed by Velopack, so the Run value survives updates.
    /// </summary>
    public static string? DefaultExePath
    {
        get
        {
            var processPath = Environment.ProcessPath;
            if (processPath is null) return null;

            var folder = Path.GetDirectoryName(processPath);
            if (!string.Equals(Path.GetFileName(folder), "current", StringComparison.OrdinalIgnoreCase)) return processPath;

            var stub = Path.Combine(Path.GetDirectoryName(folder)!, Path.GetFileName(processPath));
            return File.Exists(stub) ? stub : processPath;
        }
    }

    /// <summary>
    ///     Starts the Daemon with Windows, in the background.
    /// </summary>
    public static void Enable(string? exePath = null)
    {
        exePath ??= DefaultExePath;
        if (exePath is null) return;

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.SetValue(AppConstants.AutoStartValueName, $"\"{exePath}\" {IpcConstants.DaemonArgs.Background}");
    }

    /// <summary>
    ///     Writes the Run value again if it is on, so a value from an earlier version also gets the background argument.
    /// </summary>
    public static void Refresh()
    {
        if (IsEnabled) Enable();
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(AppConstants.AutoStartValueName, throwOnMissingValue: false);
    }
}
