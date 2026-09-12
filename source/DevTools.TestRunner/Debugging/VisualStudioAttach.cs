using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using EnvDTE;
using DteProcess = EnvDTE.Process;
using DteProcesses = EnvDTE.Processes;
namespace DevTools.TestRunner.Debugging;

public sealed class VisualStudioAttach : IDebuggerAttach
{
    public static VisualStudioAttach Instance { get; } = new();

    internal static TimeSpan AttachTimeout { get; } = TimeSpan.FromSeconds(15);

    public bool TryAttach(AttachTarget target, TextWriter warnings)
    {
        try
        {
            var dte = SelectDte(EnumerateRunningDte(), target.ParentProcessId)
                ?? GetActiveDteFallback();
            if (dte is null)
            {
                warnings.WriteLine(
                    "Visual Studio debugger was not found; host tests will run without an attached debugger.");
                return false;
            }

            var process = FindLocalProcess(dte, target.HostProcessId);
            if (process is null)
            {
                warnings.WriteLine(
                    $"Visual Studio does not list host process {target.HostProcessId}; skipping debugger attach.");
                return false;
            }

            process.Attach();
            if (WaitUntilDebugging(dte, target.HostProcessId, AttachTimeout))
                return true;

            warnings.WriteLine(
                $"Visual Studio did not confirm attach to host PID {target.HostProcessId} within {AttachTimeout.TotalSeconds:0}s.");
            return false;
        }
        catch (Exception ex)
        {
            warnings.WriteLine($"Failed to attach Visual Studio to host PID {target.HostProcessId}: {ex.Message}");
            return false;
        }
    }

    private static DTE? SelectDte(List<DTE> instances, int? parentProcessId)
    {
        if (parentProcessId is null)
            return instances.Count > 0 ? instances[0] : null;

        foreach (var t in instances.Where(t => IsDebugging(t, parentProcessId.Value)))
        {
            return t;
        }

        return instances.Count > 0 ? instances[0] : null;
    }

    private static List<DTE> EnumerateRunningDte()
    {
        var instances = new List<DTE>();
        if (OleAut32.GetRunningObjectTable(0, out var rot) != 0)
            return instances;

        rot.EnumRunning(out var enumerator);

        enumerator.Reset();
        var monikers = new IMoniker[1];
        while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
        {
            if (TryGetVisualStudioDte(rot, monikers[0], out var dte) && dte is not null)
                instances.Add(dte);
        }

        return instances;
    }

    private static bool TryGetVisualStudioDte(
        IRunningObjectTable rot,
        IMoniker moniker,
        out DTE? dte)
    {
        dte = null;
        if (OleAut32.CreateBindCtx(0, out var context) != 0)
            return false;

        try
        {
            moniker.GetDisplayName(context, null, out var name);
            if (string.IsNullOrWhiteSpace(name)
                || name.IndexOf("VisualStudio.DTE", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            rot.GetObject(moniker, out var obj);
            dte = obj as DTE;
            return dte is not null;
        }
        catch (COMException)
        {
            // Skip entries the ROT cannot bind.
            return false;
        }
        finally
        {
            Marshal.ReleaseComObject(context);
        }
    }

    private static DTE? GetActiveDteFallback()
    {
        for (var version = 23; version >= 9; version--)
        {
            try
            {
                if (OleAut32.GetActiveObject($"VisualStudio.DTE.{version}.0") is DTE dte)
                    return dte;
            }
            catch (COMException)
            {
                // Version not running.
            }
        }

        return null;
    }

    private static DteProcess? FindLocalProcess(DTE dte, int processId) =>
        FindProcess(dte.Debugger.LocalProcesses, processId);

    private static bool IsDebugging(DTE dte, int processId)
    {
        try
        {
            return FindProcess(dte.Debugger.DebuggedProcesses, processId) is not null;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static DteProcess? FindProcess(DteProcesses processes, int processId)
    {
        for (short index = 1; index <= processes.Count; index++)
        {
            var process = processes.Item(index);
            if (process.ProcessID == processId)
                return process;
        }

        return null;
    }

    private static bool WaitUntilDebugging(DTE dte, int processId, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (IsDebugging(dte, processId))
                return true;

            System.Threading.Thread.Sleep(200);
        }

        return IsDebugging(dte, processId);
    }
}
