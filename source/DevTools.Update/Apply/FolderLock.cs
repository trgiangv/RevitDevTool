using System.Runtime.InteropServices;

namespace DevTools.Update.Apply;

/// <summary>
///     A process that holds a file in a payload folder.
/// </summary>
public sealed record LockingProcess(int ProcessId, string Name);

public interface IFolderLock
{
    /// <summary>
    ///     Gets the processes that hold a top-level <c>*.dll</c> in <paramref name="folder"/>. An empty list means the folder is free.
    /// </summary>
    IReadOnlyList<LockingProcess> GetLockers(string folder);
}

/// <summary>
///     Finds folder locks with Windows Restart Manager (0049). The lock unit is the payload folder. The host type is not important.
/// </summary>
public sealed class RestartManagerFolderLock : IFolderLock
{
    public IReadOnlyList<LockingProcess> GetLockers(string folder)
    {
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly) : [];
        if (files.Length == 0) return [];

        using var session = new Session();
        session.Register(files);
        return session.GetProcesses();
    }

    private sealed class Session : IDisposable
    {
        private const int ErrorMoreData = 234;
        private readonly uint _handle;

        public Session() => Check(RmStartSession(out _handle, 0, Guid.NewGuid().ToString("N")), "start a session");

        public void Register(string[] files) =>
            Check(RmRegisterResources(_handle, (uint)files.Length, files, 0, null, 0, null), "register the files");

        public LockingProcess[] GetProcesses()
        {
            uint count = 0;
            var infos = Array.Empty<RmProcessInfo>();
            int result;
            while ((result = RmGetList(_handle, out var needed, ref count, infos, out _)) == ErrorMoreData)
            {
                infos = new RmProcessInfo[needed];
                count = needed;
            }

            Check(result, "list the processes");
            return infos.Take((int)count).Select(info => new LockingProcess(info.Process.ProcessId, info.AppName)).ToArray();
        }

        public void Dispose() => RmEndSession(_handle);

        private static void Check(int result, string action)
        {
            if (result != 0) throw new InvalidOperationException($"Restart Manager could not {action} (error {result}).");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public int ProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ServiceShortName;

        public int ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint pSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames,
        uint nApplications, RmUniqueProcess[]? rgApplications, uint nServices, string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo,
        [In, Out] RmProcessInfo[] rgAffectedApps, out uint lpdwRebootReasons);
}
