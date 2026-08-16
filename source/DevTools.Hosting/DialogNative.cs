using System.Runtime.InteropServices;

namespace DevTools.Hosting;

internal static partial class DialogNative
{
    internal const uint BmClick = 0x00F5;
    private const uint WmGetText = 0x000D;
    private const uint WmGetTextLength = 0x000E;
    private const string User32 = "user32.dll";

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [Flags]
    private enum SendMessageTimeoutFlags : uint
    {
        SMTO_BLOCK = 0x0001,
        SMTO_ABORTIFHUNG = 0x0002
    }

#if NET
    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(IntPtr hWnd);

    [LibraryImport(User32)]
    internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [LibraryImport(User32, EntryPoint = "SendMessageW")]
    internal static partial IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport(User32, EntryPoint = "SendMessageW")]
    private static unsafe partial IntPtr SendMessageBuffer(IntPtr hWnd, uint msg, IntPtr wParam, char* lParam);

    [LibraryImport(User32, EntryPoint = "GetClassNameW")]
    private static unsafe partial int GetClassNameCore(IntPtr hWnd, char* lpClassName, int nMaxCount);

    [LibraryImport(User32, EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static partial IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        SendMessageTimeoutFlags flags,
        uint timeoutMs,
        out IntPtr result);
#else
    [DllImport(User32)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport(User32)]
    internal static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport(User32)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport(User32)]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport(User32, EntryPoint = "SendMessageW")]
    internal static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport(User32, EntryPoint = "SendMessageW")]
    private static unsafe extern IntPtr SendMessageBuffer(IntPtr hWnd, uint msg, IntPtr wParam, char* lParam);

    [DllImport(User32, EntryPoint = "GetClassNameW")]
    private static unsafe extern int GetClassNameCore(IntPtr hWnd, char* lpClassName, int nMaxCount);

    [DllImport(User32, EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        SendMessageTimeoutFlags flags,
        uint timeoutMs,
        out IntPtr result);
#endif

    internal static bool TrySendMessageTimeout(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, TimeSpan timeout)
    {
        const SendMessageTimeoutFlags flags =
            SendMessageTimeoutFlags.SMTO_ABORTIFHUNG | SendMessageTimeoutFlags.SMTO_BLOCK;
        var timeoutMs = (uint)Math.Max(1, timeout.TotalMilliseconds);

        var callResult = SendMessageTimeout(hWnd, msg, wParam, lParam, flags, timeoutMs, out _);
        if (callResult != IntPtr.Zero)
            return true;

        return Marshal.GetLastWin32Error() == 0;
    }

    internal static unsafe string GetWindowText(IntPtr hwnd)
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

    internal static unsafe string GetClassName(IntPtr hwnd)
    {
        var buffer = new char[256];
        fixed (char* pBuffer = buffer)
        {
            var len = GetClassNameCore(hwnd, pBuffer, buffer.Length);
            return len > 0 ? new string(buffer, 0, len) : string.Empty;
        }
    }
}
