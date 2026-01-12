using Autodesk.Windows;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using UIFramework;
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToExtensionBlock

namespace RevitDevTool.Utils;

public static class Win32Utils
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
#pragma warning disable SYSLIB1054
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
#pragma warning restore SYSLIB1054

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, SetLastError = true)]
#pragma warning disable SYSLIB1054
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
#pragma warning restore SYSLIB1054

    [DllImport("user32.dll")]
#pragma warning disable SYSLIB1054
    private static extern bool SetForegroundWindow(IntPtr hWnd);
#pragma warning restore SYSLIB1054

    [DllImport("user32.dll", SetLastError = true)]
#pragma warning disable SYSLIB1054
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
#pragma warning restore SYSLIB1054

    [DllImport("user32.dll")]
#pragma warning disable SYSLIB1054
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
#pragma warning restore SYSLIB1054

    private const int GWL_STYLE = -16;
    private const int WS_MAXIMIZEBOX = 0x10000;
    private const int WS_MINIMIZEBOX = 0x20000;

    public static bool IsImmersiveDarkModeSupported { get; } =
        Environment.OSVersion.Version.Build >= 19041;

    public static void SetImmersiveDarkMode(this Window window, bool enable)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) return;
        SetImmersiveDarkMode(helper.Handle, enable);
    }

    public static void SetImmersiveDarkMode(IntPtr hwnd, bool isDark)
    {
        if (hwnd == IntPtr.Zero) return;
        _ = SetWindowTheme(hwnd, isDark ? "DarkMode_Explorer" : "Explorer", null);
        if (!IsImmersiveDarkModeSupported)
        {
            return;
        }

        var useDarkMode = isDark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
    }

    public static void DisableMinimizeMaximizeButtons(this Window window)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) return;

        var currentStyle = GetWindowLong(helper.Handle, GWL_STYLE);
        _ = SetWindowLong(helper.Handle, GWL_STYLE, currentStyle & ~WS_MAXIMIZEBOX & ~WS_MINIMIZEBOX);
    }

    public static void SetRevitOwner(this Window window)
    {
        window.Owner = MainWindow.getMainWnd();
        window.Closed += (EventHandler)((_, _) => SetForegroundWindow(ComponentManager.ApplicationWindow));
    }
}