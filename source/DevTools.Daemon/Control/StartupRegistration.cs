using DevTools.Ipc;
using Microsoft.Win32;
using System.IO;

namespace DevTools.Daemon.Control;

/// <summary>
///     The <see cref="AppConstants.AutoStartValueName"/> value under an HKCU key; <see cref="CurrentUserRun"/> starts the Daemon with Windows.
/// </summary>
public sealed class StartupRegistration(string keyPath)
{
    public static StartupRegistration CurrentUserRun { get; } = new(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");

    public string KeyPath { get; } = keyPath;

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
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
    ///     Starts the Daemon in the background; creates the key when it is missing.
    /// </summary>
    public void Enable(string? exePath = null)
    {
        exePath ??= DefaultExePath;
        if (exePath is null) return;

        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(AppConstants.AutoStartValueName, $"\"{exePath}\" {IpcConstants.DaemonArgs.Background}");
    }

    /// <summary>
    ///     Writes the value again if it is on, so a value from an earlier version also gets the background argument.
    /// </summary>
    public void Refresh()
    {
        if (IsEnabled) Enable();
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(AppConstants.AutoStartValueName, throwOnMissingValue: false);
    }
}
