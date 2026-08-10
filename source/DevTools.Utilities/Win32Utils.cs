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
    private const string DWMAPI_DLL = "dwmapi.dll";
    private const string USER32_DLL = "user32.dll";

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int GWL_STYLE = -16;
    private const int WS_MAXIMIZEBOX = 0x10000;
    private const int WS_MINIMIZEBOX = 0x20000;

    private const uint SC_CLOSE = 0xF060;
    private const uint MF_BYCOMMAND = 0x00000000;
    private const uint MF_GRAYED = 0x00000001;
    private const uint MF_DISABLED = 0x00000002;

    internal const uint BmClick = 0x00F5;
    private const uint WmGetText = 0x000D;
    private const uint WmGetTextLength = 0x000E;
    public const string DialogClass = "#32770";
    public const string ButtonClass = "button";

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [Flags]
    private enum SendMessageTimeoutFlags : uint
    {
        SMTO_NORMAL = 0x0000,
        SMTO_BLOCK = 0x0001,
        SMTO_ABORTIFHUNG = 0x0002,
        SMTO_NOTIMEOUTIFNOTHUNG = 0x0008,
        SMTO_ERRORONEXIT = 0x0020
    }

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

    [LibraryImport(USER32_DLL)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport(USER32_DLL)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport(USER32_DLL)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(IntPtr hWnd);

    [LibraryImport(USER32_DLL)]
    internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [LibraryImport(USER32_DLL, EntryPoint = "SendMessageW")]
    internal static partial IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport(USER32_DLL, EntryPoint = "SendMessageW")]
    private static unsafe partial IntPtr SendMessageBuffer(IntPtr hWnd, uint msg, IntPtr wParam, char* lParam);

    [LibraryImport(USER32_DLL, EntryPoint = "GetClassNameW")]
    private static unsafe partial int GetClassNameCore(IntPtr hWnd, char* lpClassName, int nMaxCount);

    [LibraryImport(USER32_DLL, EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static partial IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        SendMessageTimeoutFlags flags,
        uint timeoutMs,
        out IntPtr result);
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

    [DllImport(USER32_DLL)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport(USER32_DLL)]
    internal static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport(USER32_DLL)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport(USER32_DLL)]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport(USER32_DLL, EntryPoint = "SendMessageW")]
    internal static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport(USER32_DLL, EntryPoint = "SendMessageW")]
    private static unsafe extern IntPtr SendMessageBuffer(IntPtr hWnd, uint msg, IntPtr wParam, char* lParam);

    [DllImport(USER32_DLL, EntryPoint = "GetClassNameW")]
    private static unsafe extern int GetClassNameCore(IntPtr hWnd, char* lpClassName, int nMaxCount);

    [DllImport(USER32_DLL, EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        SendMessageTimeoutFlags flags,
        uint timeoutMs,
        out IntPtr result);
#endif

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

    public static bool TrySendMessageTimeout(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, TimeSpan timeout)
    {
        const SendMessageTimeoutFlags flags =
            SendMessageTimeoutFlags.SMTO_ABORTIFHUNG | SendMessageTimeoutFlags.SMTO_BLOCK;
        var timeoutMs = (uint)Math.Max(1, timeout.TotalMilliseconds);

        var callResult = SendMessageTimeout(hWnd, msg, wParam, lParam, flags, timeoutMs, out _);

        if (callResult != IntPtr.Zero)
            return true;

        var lastError = Marshal.GetLastWin32Error();
        return lastError == 0;
    }

    public static unsafe string GetWindowText(IntPtr hwnd)
    {
        var len = (int)SendMessage(hwnd, WmGetTextLength, IntPtr.Zero, IntPtr.Zero);
        if (len <= 0)
            return string.Empty;

        var buffer = new char[len + 1];
        fixed (char* pBuffer = buffer)
        {
            var actual = (int)SendMessageBuffer(hwnd, WmGetText, new IntPtr(len + 1), pBuffer);
            return actual > 0 ? new string(buffer, 0, actual) : string.Empty;
        }
    }

    public static unsafe string GetClassName(IntPtr hwnd)
    {
        var buffer = new char[256];
        fixed (char* pBuffer = buffer)
        {
            var len = GetClassNameCore(hwnd, pBuffer, buffer.Length);
            return len > 0 ? new string(buffer, 0, len) : string.Empty;
        }
    }
}
