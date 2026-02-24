using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace RevitDevTool.Console.Services.Hosting;

/// <summary>
/// Win32-based watcher that detects Revit crash signals early.
/// </summary>
public static class RevitCrashWatcher
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(800);
    private static readonly string[] CrashDialogKeywords =
    [
        "Error Report",
        "has stopped working",
        "close unexpectedly"
    ];

    public static async Task MonitorAsync(
        int processId,
        string hostVersion,
        CancellationToken ct,
        TimeSpan? pollInterval = null)
    {
        var interval = pollInterval ?? DefaultPollInterval;

        while (!ct.IsCancellationRequested)
        {
            ThrowIfProcessExited(processId, hostVersion);
            ThrowIfCrashDialogDetected(processId, hostVersion);
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }

    public static bool TryGetCrashSignal(int processId, string hostVersion, out string reason)
    {
        try
        {
            ThrowIfProcessExited(processId, hostVersion);
            ThrowIfCrashDialogDetected(processId, hostVersion);
            reason = string.Empty;
            return false;
        }
        catch (InvalidOperationException ex)
        {
            reason = ex.Message;
            return true;
        }
    }

    private static void ThrowIfProcessExited(int processId, string hostVersion)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            process.Refresh();
            if (process.HasExited)
                throw new InvalidOperationException(
                    $"[crash-watcher:{hostVersion}:{processId}] Revit process exited unexpectedly.");
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                $"[crash-watcher:{hostVersion}:{processId}] Revit process is no longer running.");
        }
    }

    private static void ThrowIfCrashDialogDetected(int processId, string hostVersion)
    {
        var matchingDialogTitle = FindCrashDialogTitle(processId);
        if (string.IsNullOrWhiteSpace(matchingDialogTitle))
            return;

        throw new InvalidOperationException(
            $"[crash-watcher:{hostVersion}:{processId}] Detected Revit crash dialog: \"{matchingDialogTitle}\".");
    }

    private static string? FindCrashDialogTitle(int processId)
    {
        string? hit = null;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;

            if (!string.Equals(GetClassNameValue(hwnd), "#32770", StringComparison.OrdinalIgnoreCase))
                return true;

            var title = GetWindowTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            var matchesKeyword = CrashDialogKeywords.Any(
                keyword => title.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (!matchesKeyword)
                return true;

            if (!title.Contains("Revit", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!IsDialogRelatedToRevit(hwnd, processId))
                return true;

            hit = title;
            return false;
        }, nint.Zero);

        return hit;
    }

    private static bool IsDialogRelatedToRevit(nint hwnd, int processId)
    {
        GetWindowThreadProcessId(hwnd, out var windowPid);
        if (windowPid == processId)
            return true;

        if (windowPid <= 0)
            return false;

        try
        {
            using var owner = Process.GetProcessById(windowPid);
            if (string.Equals(owner.ProcessName, "WerFault", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(owner.ProcessName, "Wermgr", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(owner.ProcessName, "Revit", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch
        {
            // Ignore inaccessible process info.
        }

        return false;
    }

    private static string GetWindowTitle(nint hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
            return string.Empty;

        var sb = new StringBuilder(length + 1);
        _ = GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetClassNameValue(nint hwnd)
    {
        var sb = new StringBuilder(256);
        _ = GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out int lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);
}
