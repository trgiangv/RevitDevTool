using Autodesk.Windows;
using System.Runtime.InteropServices;
using System.Windows;
using UIFramework;
namespace RevitDevTool.Utils;

public static class Win32Utils
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, SetLastError = true)]
#pragma warning disable SYSLIB1054
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
#pragma warning restore SYSLIB1054

    [DllImport("user32.dll")]
#pragma warning disable SYSLIB1054
    private static extern bool SetForegroundWindow(IntPtr hWnd);
#pragma warning restore SYSLIB1054

    public static void SetImmersiveDarkMode(IntPtr hwnd, bool isDark)
    {
        if (hwnd == IntPtr.Zero) return;
        _ = SetWindowTheme(hwnd, isDark ? "DarkMode_Explorer" : "Explorer", null);
    }

    public static void SetRevitOwner(this Window window)
    {
        window.Owner = MainWindow.getMainWnd();
        window.Closed += (EventHandler)((_, _) => SetForegroundWindow(ComponentManager.ApplicationWindow));
    }
}