using Autodesk.Windows;
using RevitDevTool.Logging.Theme;
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll")]
    private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

    private const int GWL_STYLE = -16;
    private const int WS_MAXIMIZEBOX = 0x10000;
    private const int WS_MINIMIZEBOX = 0x20000;
    
    private const uint SC_CLOSE = 0xF060;
    private const uint MF_BYCOMMAND = 0x00000000;
    private const uint MF_GRAYED = 0x00000001;
    private const uint MF_DISABLED = 0x00000002;

    public static void SetImmersiveDarkMode(this Window window, bool enable)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) return;
        SetImmersiveDarkMode(helper.Handle, enable);
    }

    private static void SetImmersiveDarkMode(IntPtr hwnd, bool isDark)
    {
        if (hwnd == IntPtr.Zero) return;
        _ = SetWindowTheme(hwnd, isDark ? "DarkMode_Explorer" : "Explorer", null);
        var useDarkMode = isDark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
    }

    /// <summary>
    /// Configures window buttons (Minimize, Maximize, Close).
    /// </summary>
    public static void DisableWindowButtons(this Window window, bool disableMinimize = true, bool disableMaximize = true, bool disableClose = false)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) return;

        var currentStyle = GetWindowLong(helper.Handle, GWL_STYLE);
        
        if (disableMinimize) currentStyle &= ~WS_MINIMIZEBOX;
        if (disableMaximize) currentStyle &= ~WS_MAXIMIZEBOX;
        
        _ = SetWindowLong(helper.Handle, GWL_STYLE, currentStyle);

        if (disableClose)
        {
            IntPtr hMenu = GetSystemMenu(helper.Handle, false);
            if (hMenu != IntPtr.Zero)
            {
                EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED | MF_DISABLED);
            }
        }
    }

    public static void SetRevitOwner(this Window window)
    {
        window.Owner = MainWindow.getMainWnd();
        window.Closed += (EventHandler)((_, _) => SetForegroundWindow(ComponentManager.ApplicationWindow));
    }

    public static void SetRichTextBoxTheme(this RichTextBox richTextBox, bool isDarkTheme)
    {
        richTextBox.BackColor = isDarkTheme
            ? LogThemePresets.DarkBackground
            : LogThemePresets.LightBackground;
        SetImmersiveDarkMode(richTextBox.Handle, isDarkTheme);
    }
}