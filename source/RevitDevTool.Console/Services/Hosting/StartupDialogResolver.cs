using System.Runtime.InteropServices;
using System.Text;

namespace RevitDevTool.Console.Services.Hosting;

/// <summary>
/// External-first startup dialog resolver based on Win32 polling.
/// Handles dialogs that may appear before add-in code starts inside Revit.
/// </summary>
public sealed class StartupDialogResolver : IStartupDialogResolver
{
    private const uint BmClick = 0x00F5;
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
    private readonly StartupDialogResolverOptions _options;

    public StartupDialogResolver(StartupDialogResolverOptions? options = null)
    {
        _options = options ?? new StartupDialogResolverOptions();
    }

    public async Task RunAsync(int processId, string hostVersion, CancellationToken ct = default)
    {
        var state = new ResolverState();

        while (!ct.IsCancellationRequested)
        {
            ScanAndResolveDialogs(processId, hostVersion, state);

            await Task.Delay(_options.PollInterval, ct).ConfigureAwait(false);
        }
    }

    private void ScanAndResolveDialogs(int processId, string hostVersion, ResolverState state)
    {
        foreach (var hwnd in EnumerateTopLevelWindowsByProcess(processId))
        {
            ResolveSingleDialog(hwnd, processId, hostVersion, state);
        }
    }

    private void ResolveSingleDialog(nint hwnd, int processId, string hostVersion, ResolverState state)
    {
        var title = GetWindowTitle(hwnd);
        if (string.IsNullOrWhiteSpace(title) || !IsWhitelistedTitle(title))
            return;

        var button = FindWhitelistedButton(hwnd);
        if (button == nint.Zero)
        {
            HandleNoButtonFound(hwnd, title, processId, hostVersion, state);
            return;
        }

        ClickButtonIfNeeded(button, title, processId, hostVersion, state);
    }

    private void HandleNoButtonFound(
        nint hwnd,
        string title,
        int processId,
        string hostVersion,
        ResolverState state)
    {
        state.NoButtonRetriesByWindow.TryGetValue(hwnd, out var retries);
        retries++;
        state.NoButtonRetriesByWindow[hwnd] = retries;

        System.Console.WriteLine(
            $"[startup-dialog-resolver] detected Revit {hostVersion} dialog (PID {processId}): \"{title}\" (no safe button found, retry {retries}/{_options.MaxNoButtonRetriesPerWindow})");

        if (retries < _options.MaxNoButtonRetriesPerWindow)
            return;

        throw new InvalidOperationException(
            $"Startup dialog resolver detected a blocking dialog but could not find a whitelisted button after {retries} retries: \"{title}\".");
    }

    private static void ClickButtonIfNeeded(
        nint button,
        string title,
        int processId,
        string hostVersion,
        ResolverState state)
    {
        if (!state.ClickedButtons.Add(button))
        {
            System.Console.WriteLine(
                $"[startup-dialog-resolver] ignored already-clicked button for Revit {hostVersion} (PID {processId}): \"{title}\"");
            return;
        }

        SendMessage(button, BmClick, nint.Zero, nint.Zero);
        System.Console.WriteLine(
            $"[startup-dialog-resolver] clicked startup dialog button for Revit {hostVersion} (PID {processId}): \"{title}\"");
    }

    private bool IsWhitelistedTitle(string title)
    {
        foreach (var keyword in _options.DialogTitleKeywords)
        {
            if (title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private nint FindWhitelistedButton(nint parentWindow)
    {
        nint matchedButton = nint.Zero;
        EnumChildWindows(parentWindow, (child, _) =>
        {
            if (!Comparer.Equals(GetClassNameValue(child), "Button"))
                return true;

            var text = GetWindowTitle(child);
            if (string.IsNullOrWhiteSpace(text))
                return true;

            foreach (var keyword in _options.PreferredButtonKeywords)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    matchedButton = child;
                    return false;
                }
            }
            
            return true;
        }, nint.Zero);

        return matchedButton;
    }

    private static List<nint> EnumerateTopLevelWindowsByProcess(int processId)
    {
        var windows = new List<nint>();

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;

            if (!Comparer.Equals(GetClassNameValue(hwnd), "#32770"))
                return true;

            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != processId)
                return true;

            windows.Add(hwnd);
            return true;
        }, nint.Zero);

        return windows;
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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(nint hWndParent, EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    private sealed class ResolverState
    {
        public Dictionary<nint, int> NoButtonRetriesByWindow { get; } = new();
        public HashSet<nint> ClickedButtons { get; } = [];
    }
}
