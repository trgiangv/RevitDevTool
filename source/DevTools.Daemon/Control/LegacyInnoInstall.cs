using DevTools.Update.Autodesk;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace DevTools.Daemon.Control;

/// <summary>
///     Removes the 3.0.0 Inno Setup install that the Velopack install replaces (0054). The payload folders stay;
///     the Updates tab offers them as the default selection.
/// </summary>
internal static class LegacyInnoInstall
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{B2BC2881-A08A-41D8-B1B3-424045E529DB}_is1";
    private static readonly string[] Shortcuts = ["RevitDevTool Dashboard.lnk", "Uninstall RevitDevTool.lnk"];
    private static readonly string[] Executables = ["DevTools.Daemon.exe", "DevTools.TestRunner.exe"];

    public static bool Exists
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
            return key is not null;
        }
    }

    /// <summary>
    ///     Stops the old Daemon processes, because the old tray Daemon holds the single-instance mutex.
    ///     Then removes the old executables, the uninstaller, the Start menu shortcuts, and the uninstall entry.
    /// </summary>
    public static void Remove(AutodeskBundle bundle)
    {
        if (!Exists) return;

        StopDaemons(Path.Combine(bundle.Contents, Executables[0]));
        foreach (var executable in Executables.Select(name => Path.Combine(bundle.Contents, name)).Where(File.Exists))
            File.Delete(executable);

        var uninstaller = Path.Combine(bundle.Root, "Uninstall");
        if (Directory.Exists(uninstaller)) Directory.Delete(uninstaller, recursive: true);

        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        foreach (var shortcut in Shortcuts.Select(name => Path.Combine(programs, name)).Where(File.Exists))
            File.Delete(shortcut);

        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey);
    }

    private static void StopDaemons(string legacyPath)
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(legacyPath)))
        {
            using (process)
            {
                if (!string.Equals(process.MainModule?.FileName, legacyPath, StringComparison.OrdinalIgnoreCase)) continue;

                process.Kill();
                process.WaitForExit();
            }
        }
    }
}
