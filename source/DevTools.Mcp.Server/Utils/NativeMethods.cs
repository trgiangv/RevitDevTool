using System.Runtime.InteropServices;

namespace DevTools.Mcp.Server.Utils;

internal static partial class NativeMethods
{
    internal const uint BmClick = 0x00F5;
    private const uint WmGetText = 0x000D;
    private const uint WmGetTextLength = 0x000E;
    public const string Dialog = "#32770";
    public const string Button = "button";
    private const string User32 = "user32.dll";

    internal delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumChildWindows(nint hWndParent, EnumWindowsProc lpEnumFunc, nint lParam);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint hWnd);

    [LibraryImport(User32)]
    internal static partial uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [LibraryImport(User32, EntryPoint = "SendMessageW")]
    internal static partial nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport(User32, EntryPoint = "SendMessageW")]
    private static unsafe partial nint SendMessageBuffer(nint hWnd, uint msg, nint wParam, char* lParam);

    [LibraryImport(User32, EntryPoint = "GetClassNameW")]
    private static unsafe partial int GetClassNameCore(nint hWnd, char* lpClassName, int nMaxCount);

    [Flags]
    private enum SendMessageTimeoutFlags : uint
    {
        SMTO_NORMAL = 0x0000,
        SMTO_BLOCK = 0x0001,
        SMTO_ABORTIFHUNG = 0x0002,
        SMTO_NOTIMEOUTIFNOTHUNG = 0x0008,
        SMTO_ERRORONEXIT = 0x0020
    }

    [LibraryImport(User32, EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static partial nint SendMessageTimeout(
        nint hWnd,
        uint msg,
        nint wParam,
        nint lParam,
        SendMessageTimeoutFlags flags,
        uint timeoutMs,
        out nint result);

    public static bool TrySendMessageTimeout(
        nint hWnd, uint msg, nint wParam, nint lParam, TimeSpan timeout)
    {
        const SendMessageTimeoutFlags flags = SendMessageTimeoutFlags.SMTO_ABORTIFHUNG | SendMessageTimeoutFlags.SMTO_BLOCK;
        var timeoutMs = (uint)Math.Max(1, timeout.TotalMilliseconds);

        var callResult = SendMessageTimeout(hWnd, msg, wParam, lParam, flags, timeoutMs, out _);

        if (callResult != 0)
            return true;

        var lastError = Marshal.GetLastWin32Error();
        return lastError == 0;
    }

    internal static unsafe string GetWindowText(nint hwnd)
    {
        var len = (int)SendMessage(hwnd, WmGetTextLength, 0, 0);
        if (len <= 0)
            return string.Empty;

        var buffer = new char[len + 1];
        fixed (char* pBuffer = buffer)
        {
            var actual = (int)SendMessageBuffer(hwnd, WmGetText, len + 1, pBuffer);
            return actual > 0 ? new string(buffer, 0, actual) : string.Empty;
        }
    }

    internal static unsafe string GetClassName(nint hwnd)
    {
        var buffer = new char[256];
        fixed (char* pBuffer = buffer)
        {
            var len = GetClassNameCore(hwnd, pBuffer, buffer.Length);
            return len > 0 ? new string(buffer, 0, len) : string.Empty;
        }
    }
}
