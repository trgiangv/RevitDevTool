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
    private enum SendMessageTimeoutOption : uint
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
    private static partial IntPtr SendMessageBuffer(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport(User32, EntryPoint = "GetClassNameW")]
    private static partial int GetClassNameCore(IntPtr hWnd, IntPtr lpClassName, int nMaxCount);

    [LibraryImport(User32, EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static partial IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        SendMessageTimeoutOption flags,
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
    private static extern IntPtr SendMessageBuffer(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport(User32, EntryPoint = "GetClassNameW")]
    private static extern int GetClassNameCore(IntPtr hWnd, IntPtr lpClassName, int nMaxCount);

    [DllImport(User32, EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        SendMessageTimeoutOption flags,
        uint timeoutMs,
        out IntPtr result);
#endif

    internal static bool TrySendMessageTimeout(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, TimeSpan timeout)
    {
        const SendMessageTimeoutOption flags =
            SendMessageTimeoutOption.SMTO_ABORTIFHUNG | SendMessageTimeoutOption.SMTO_BLOCK;
        var timeoutMs = (uint)Math.Max(1, timeout.TotalMilliseconds);

        var callResult = SendMessageTimeout(hWnd, msg, wParam, lParam, flags, timeoutMs, out _);
        if (callResult != IntPtr.Zero)
            return true;

        return Marshal.GetLastWin32Error() == 0;
    }

    internal static string GetWindowText(IntPtr hwnd)
    {
        var len = (int)SendMessage(hwnd, WmGetTextLength, IntPtr.Zero, IntPtr.Zero);
        if (len <= 0)
            return string.Empty;

        var buffer = Marshal.AllocHGlobal((len + 1) * sizeof(char));
        try
        {
            var actual = (int)SendMessageBuffer(hwnd, WmGetText, new IntPtr(len + 1), buffer);
            return actual > 0 ? Marshal.PtrToStringUni(buffer, actual) ?? string.Empty : string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static string GetClassName(IntPtr hwnd)
    {
        const int bufferLength = 256;
        var buffer = Marshal.AllocHGlobal(bufferLength * sizeof(char));
        try
        {
            var len = GetClassNameCore(hwnd, buffer, bufferLength);
            return len > 0 ? Marshal.PtrToStringUni(buffer, len) ?? string.Empty : string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
