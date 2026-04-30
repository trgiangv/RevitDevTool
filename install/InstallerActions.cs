using System.Diagnostics;
using System.Runtime.InteropServices;
using WixToolset.Dtf.WindowsInstaller;

namespace Installer;

public static class InstallerActions
{
    private static readonly (string Contains, string DisplayName)[] ManagedApps =
    [
        ("revit", "Autodesk Revit"),
        ("acad", "Autodesk AutoCAD"),
    ];

    [CustomAction]
    public static ActionResult DetectRunningApps(Session session)
    {
        var allProcessNames = Process.GetProcesses().Select(p => p.ProcessName);
        var running = ManagedApps
            .Where(app => allProcessNames.Any(p => p.Contains(app.Contains, StringComparison.OrdinalIgnoreCase)))
            .Select(app => app.DisplayName)
            .ToList();

        if (running.Count == 0)
            return ActionResult.Success;

        var appList = string.Join("\n  - ", running);
        var message = $"The following applications are currently open:\n\n  - {appList}\n\nPlease close them before continuing installation.\n\nClick OK to continue anyway, or Cancel to abort.";

        var result = MessageBox(IntPtr.Zero, message, "RevitDevTool Installer", MB_OKCANCEL | MB_ICONWARNING | MB_TOPMOST);
        if (result == IDCANCEL)
        {
            session.Log("User cancelled installation due to running apps.");
            return ActionResult.UserExit;
        }

        return ActionResult.Success;
    }

    [CustomAction]
    public static ActionResult CleanBundleFolder(Session session)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        CleanIfExists(session, Path.Combine(appData, @"Autodesk\ApplicationPlugins\RevitDevTool.bundle"));
        CleanIfExists(session, Path.Combine(programData, @"Autodesk\ApplicationPlugins\RevitDevTool.bundle"));

        return ActionResult.Success;
    }

    private static void CleanIfExists(Session session, string path)
    {
        if (!Directory.Exists(path)) return;

        try
        {
            Directory.Delete(path, recursive: true);
            session.Log($"Cleaned bundle folder: {path}");
        }
        catch (Exception ex)
        {
            session.Log($"Warning: could not clean '{path}': {ex.Message}");
        }
    }

    private const int MB_OKCANCEL = 0x00000001;
    private const int MB_ICONWARNING = 0x00000030;
    private const int MB_TOPMOST = 0x00040000;
    private const int IDCANCEL = 2;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, int type);
}
