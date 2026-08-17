using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using EnvDTE;

namespace DevTools.TestRunner.Core.Debugging;

public sealed class VisualStudioAttach : IVisualStudioAttach
{
    public static VisualStudioAttach Instance { get; } = new();

    internal static TimeSpan AttachTimeout { get; } = TimeSpan.FromSeconds(15);

    public bool TryAttach(int hostProcessId, int? parentProcessId, TextWriter warnings)
    {
        try
        {
            var dte = FindDte(parentProcessId);
            if (dte is null)
            {
                warnings.WriteLine(
                    "Visual Studio debugger was not found; host tests will run without an attached debugger.");
                return false;
            }

            var process = FindLocalProcess(dte, hostProcessId);
            if (process is null)
            {
                warnings.WriteLine(
                    $"Visual Studio does not list host process {hostProcessId}; skipping debugger attach.");
                return false;
            }

            process.Attach();
            if (WaitUntilDebugging(dte, hostProcessId, AttachTimeout))
                return true;

            warnings.WriteLine(
                $"Visual Studio did not confirm attach to host PID {hostProcessId} within {AttachTimeout.TotalSeconds:0}s.");
            return false;
        }
        catch (Exception ex)
        {
            warnings.WriteLine($"Failed to attach Visual Studio to host PID {hostProcessId}: {ex.Message}");
            return false;
        }
    }

    public void TryDetach(int hostProcessId, TextWriter warnings)
    {
        try
        {
            var dte = EnumerateRunningDte().FirstOrDefault(candidate => IsDebugging(candidate, hostProcessId));
            var process = dte is null ? null : FindLocalProcess(dte, hostProcessId);
            process?.Detach(false);
        }
        catch (Exception ex)
        {
            warnings.WriteLine($"Failed to detach Visual Studio from host PID {hostProcessId}: {ex.Message}");
        }
    }

    private static DTE? FindDte(int? parentProcessId)
    {
        var instances = EnumerateRunningDte();
        if (parentProcessId is int pid)
        {
            var match = instances.FirstOrDefault(dte => IsDebugging(dte, pid));
            if (match is not null)
                return match;
        }

        return instances.FirstOrDefault() ?? GetActiveDteFallback();
    }

    private static IReadOnlyList<DTE> EnumerateRunningDte()
    {
        var instances = new List<DTE>();
        if (GetRunningObjectTable(0, out var rot) != 0 || rot is null)
            return instances;

        rot.EnumRunning(out var enumerator);
        if (enumerator is null)
            return instances;

        enumerator.Reset();
        var monikers = new IMoniker[1];
        while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
        {
            IBindCtx? ctx = null;
            try
            {
                CreateBindCtx(0, out ctx);
                monikers[0].GetDisplayName(ctx, null, out var name);
                if (string.IsNullOrWhiteSpace(name)
                    || name.IndexOf("VisualStudio.DTE", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                rot.GetObject(monikers[0], out var obj);
                if (obj is DTE dte)
                    instances.Add(dte);
            }
            catch (COMException)
            {
                // Skip entries the ROT cannot bind.
            }
            finally
            {
                if (ctx is not null)
                    Marshal.ReleaseComObject(ctx);
            }
        }

        return instances;
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

    private static EnvDTE.Process? FindLocalProcess(DTE dte, int processId) =>
        dte.Debugger.LocalProcesses.OfType<EnvDTE.Process>()
            .FirstOrDefault(process => process.ProcessID == processId);

    private static bool IsDebugging(DTE dte, int processId)
    {
        try
        {
            return dte.Debugger.DebuggedProcesses.OfType<EnvDTE.Process>()
                .Any(process => process.ProcessID == processId);
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static bool WaitUntilDebugging(DTE dte, int processId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (IsDebugging(dte, processId))
                return true;

            System.Threading.Thread.Sleep(200);
        }

        return IsDebugging(dte, processId);
    }

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable pprot);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);
}
