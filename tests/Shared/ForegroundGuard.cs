using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DevTools.Testing.Ui;

// UI tests show real windows. This keeps the user's window in front and keeps their keyboard focus:
// a test window that reaches the foreground gives it straight back, whatever made it come forward.
internal static class ForegroundGuard
{
    private const uint ForegroundEvent = 0x0003;
    private const uint OutOfContext = 0x0000;
    private const int ExStyle = -20;
    private const int NoActivate = 0x08000000;

    private static readonly int ProcessId = Process.GetCurrentProcess().Id;
    private static WinEventProc? _callback;
    private static IntPtr _hook;
    private static IntPtr _userWindow;

    public static int Returned { get; private set; }

    // Call on a thread that pumps messages (the WPF dispatcher thread).
    public static void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        if (!IsOurs(GetForegroundWindow()))
        {
            _userWindow = GetForegroundWindow();
        }

        _callback = OnForeground;
        _hook = SetWinEventHook(ForegroundEvent, ForegroundEvent, IntPtr.Zero, _callback, 0, 0, OutOfContext);
    }

    public static void NoActivateOnShow(Window window)
    {
        window.ShowActivated = false;
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            SetWindowLongPtr(handle, ExStyle, new IntPtr(GetWindowLongPtr(handle, ExStyle).ToInt64() | NoActivate));
        };
    }

    private static void OnForeground(IntPtr hook, uint type, IntPtr window, int objectId, int childId, uint thread, uint time)
    {
        if (!IsOurs(window))
        {
            _userWindow = window;
            return;
        }

        if (_userWindow != IntPtr.Zero && IsWindow(_userWindow) && SetForegroundWindow(_userWindow))
        {
            Returned++;
            Trace.WriteLine($"ForegroundGuard: a test window took the foreground and gave it back ({Returned}).");
        }
    }

    private static bool IsOurs(IntPtr window)
    {
        GetWindowThreadProcessId(window, out var owner);
        return owner == ProcessId;
    }

    private delegate void WinEventProc(IntPtr hook, uint type, IntPtr window, int objectId, int childId, uint thread, uint time);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out int process);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
