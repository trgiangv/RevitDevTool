using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DevTools.UI;

public static class Win32Utils
{
    private const string DwmApiDll = "dwmapi.dll";
    private const string User32Dll = "user32.dll";

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int GwlStyle = -16;
    private const int WsMaximizeBox = 0x10000;
    private const int WsMinimizeBox = 0x20000;

    private const uint ScClose = 0xF060;
    private const uint MfByCommand = 0x00000000;
    private const uint MfGrayed = 0x00000001;
    private const uint MfDisabled = 0x00000002;

    [DllImport(DwmApiDll, SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport(User32Dll)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport(User32Dll, EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport(User32Dll, EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport(User32Dll, SetLastError = true)]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport(User32Dll)]
    private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

    public static void SetTitleBarTheme(this Window window, bool isDark)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) return;

        var useDarkMode = isDark ? 1 : 0;
        _ = DwmSetWindowAttribute(helper.Handle, DwmwaUseImmersiveDarkMode, ref useDarkMode, sizeof(int));
    }

    /// <summary>
    /// Configures window buttons (Minimize, Maximize, Close).
    /// </summary>
    public static void SetWindowButtons(
        this Window window,
        bool disableMinimize = true,
        bool disableMaximize = true,
        bool disableClose = false)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) return;
        var currentStyle = (int)GetWindowLongPtr(helper.Handle, GwlStyle);
        if (disableMinimize) currentStyle &= ~WsMinimizeBox;
        if (disableMaximize) currentStyle &= ~WsMaximizeBox;
        _ = SetWindowLongPtr(helper.Handle, GwlStyle, new IntPtr(currentStyle));

        if (!disableClose) return;

        var hMenu = GetSystemMenu(helper.Handle, false);
        if (hMenu != IntPtr.Zero)
            EnableMenuItem(hMenu, ScClose, MfByCommand | MfGrayed | MfDisabled);
    }

    public static void SetHostAppOwner(this Window window)
    {
        new WindowInteropHelper(window).Owner = HostUiHelper.MainWindowHandle;
        window.Closed += (_, _) => SetForegroundWindow(HostUiHelper.MainWindowHandle);
    }
}
