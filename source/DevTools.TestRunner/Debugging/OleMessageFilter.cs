using System.Runtime.InteropServices;

namespace DevTools.TestRunner.Debugging;

/// <summary>
/// COM retry when Visual Studio returns <c>RPC_E_SERVERCALL_RETRYLATER</c>
/// (busy while debugging the testhost). Must run on STA.
/// https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2010/ms228772(v=vs.100)
/// </summary>
internal static class OleMessageFilter
{
    private const int ServerCallIshandled = 0;
    private const int ServerCallRetryLater = 2;
    private const int PendingWaitDefProcess = 2;

    public static void Register() => CoRegisterMessageFilter(new Filter(), out _);

    public static void Unregister() => CoRegisterMessageFilter(null, out _);

    [ComImport]
    [Guid("00000016-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOleMessageFilter
    {
        [PreserveSig]
        int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

        [PreserveSig]
        int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

        [PreserveSig]
        int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
    }

    private sealed class Filter : IOleMessageFilter
    {
        public int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo) =>
            ServerCallIshandled;

        public int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType) =>
            dwRejectType == ServerCallRetryLater ? 99 : -1;

        public int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType) =>
            PendingWaitDefProcess;
    }

    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);
}
