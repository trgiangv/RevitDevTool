using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToExtensionBlock
// ReSharper disable UnusedMethodReturnValue.Local

namespace DevTools.Utilities;

// ReSharper disable once PartialTypeWithSinglePart
public static partial class Win32Utils
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const string DWMAPI_DLL = "dwmapi.dll";
    private const string USER32_DLL = "user32.dll";

#if NET
    [LibraryImport(DWMAPI_DLL, EntryPoint = "DwmSetWindowAttribute", SetLastError = true)]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [LibraryImport(USER32_DLL)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport(USER32_DLL, EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [LibraryImport(USER32_DLL, EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    
    [LibraryImport(USER32_DLL, SetLastError = true)]
    private static partial IntPtr GetSystemMenu(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bRevert);

    [LibraryImport(USER32_DLL)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);
#else
    [DllImport(DWMAPI_DLL, SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport(USER32_DLL)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport(USER32_DLL, EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport(USER32_DLL, EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport(USER32_DLL, SetLastError = true)]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport(USER32_DLL)]
    private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);
#endif

    private const int GWL_STYLE = -16;
    private const int WS_MAXIMIZEBOX = 0x10000;
    private const int WS_MINIMIZEBOX = 0x20000;

    private const uint SC_CLOSE = 0xF060;
    private const uint MF_BYCOMMAND = 0x00000000;
    private const uint MF_GRAYED = 0x00000001;
    private const uint MF_DISABLED = 0x00000002;

    public static void SetTitleBarTheme(this Window window, bool isDark)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) return;

        var useDarkMode = isDark ? 1 : 0;
        _ = DwmSetWindowAttribute(helper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
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
        var currentStyle = (int)GetWindowLongPtr(helper.Handle, GWL_STYLE);
        if (disableMinimize) currentStyle &= ~WS_MINIMIZEBOX;
        if (disableMaximize) currentStyle &= ~WS_MAXIMIZEBOX;
        _ = SetWindowLongPtr(helper.Handle, GWL_STYLE, new IntPtr(currentStyle));

        if (!disableClose) return;

        var hMenu = GetSystemMenu(helper.Handle, false);
        if (hMenu != IntPtr.Zero)
            EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED | MF_DISABLED);
    }
    
    public static void SetHostAppOwner(this Window window)
    {
        new WindowInteropHelper(window).Owner = HostUiHelper.MainWindowHandle;
        window.Closed += (_, _) => SetForegroundWindow(HostUiHelper.MainWindowHandle);
    }
}
