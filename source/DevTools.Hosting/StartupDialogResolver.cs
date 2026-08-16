namespace DevTools.Hosting;

/// <summary>
/// Per-host catalog. Window/button class names and keywords come from
/// <see cref="IHostStartupDialogSpec"/>, not this engine.
/// </summary>
public sealed class StartupDialogOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Timeout for a single BM_CLICK before retrying on the next poll.</summary>
    public TimeSpan ClickTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public IReadOnlyList<string> DialogTitleKeywords { get; init; } = [];

    public IReadOnlyList<string> PreferredButtonKeywords { get; init; } = [];

    public IReadOnlyList<string> BlockedButtonKeywords { get; init; } = [];

    public string WindowClassName { get; init; } = "";

    public string ButtonClassName { get; init; } = "";
}

/// <summary>
/// Outcome of polling startup dialogs. <see cref="Resolved"/> is true when no matching
/// dialog remains. <see cref="ClickCount"/> is how many preferred buttons were clicked.
/// <see cref="Clicked"/> / <see cref="Remaining"/> are dialog titles.
/// </summary>
public sealed record StartupDialogResult(
    IReadOnlyList<string> Clicked,
    IReadOnlyList<string> Remaining)
{
    public int ClickCount => Clicked.Count;

    public bool Resolved => Remaining.Count == 0;
}

/// <summary>
/// Polls a process's top-level windows and clicks a preferred button.
/// Caller cancellation is the only stop valve — there is no self-timeout.
/// </summary>
public static class StartupDialogResolver
{
    public static Session? Start(IHostStartupDialogSpec? spec, int processId, CancellationToken cancellationToken)
    {
        if (spec is null || cancellationToken.IsCancellationRequested)
            return null;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var task = RunAsync(processId, spec.CreateOptions(), cts.Token);
        return new Session(cts, task);
    }

    public static async Task<StartupDialogResult> RunAsync(
        int processId,
        StartupDialogOptions options,
        CancellationToken cancellationToken)
    {
        var clickedButtons = new HashSet<IntPtr>();
        var clicked = new List<string>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ScanAndHandleDialogs(processId, options, clickedButtons, clicked);
                await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Caller timeout / dispose is the valve.
        }

        var remaining = new List<string>();
        foreach (var hwnd in EnumerateTopLevelWindowsForProcess(processId))
        {
            if (IsTargetDialog(hwnd, options, out var title))
                remaining.Add(title);
        }

        return new StartupDialogResult(clicked, remaining);
    }

    /// <summary>Owns the background poll. Dispose cancels it.</summary>
    public sealed class Session : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private bool _disposed;

        internal Session(CancellationTokenSource cts, Task<StartupDialogResult> completion)
        {
            _cts = cts;
            Completion = completion;
        }

        public Task<StartupDialogResult> Completion { get; }

        public async Task<StartupDialogResult?> TryGetResultAsync(TimeSpan wait)
        {
            var task = Completion;
            if (task.Status == TaskStatus.RanToCompletion)
                return task.Result;

            try
            {
                using var cts = new CancellationTokenSource(wait);
                var delay = Task.Delay(wait, cts.Token);
                var completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
                if (completed != task)
                    return null;

                return await task.ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // already disposed
            }

            _cts.Dispose();
        }
    }

    private static void ScanAndHandleDialogs(
        int processId,
        StartupDialogOptions options,
        HashSet<IntPtr> clickedButtons,
        List<string> clicked)
    {
        foreach (var hwnd in EnumerateTopLevelWindowsForProcess(processId))
        {
            if (!IsTargetDialog(hwnd, options, out var title))
                continue;

            if (!TryClickPreferredButton(hwnd, options, clickedButtons))
                continue;

            clicked.Add(title);
        }
    }

    private static bool IsTargetDialog(IntPtr hwnd, StartupDialogOptions options, out string title)
    {
        title = string.Empty;
        if (string.IsNullOrWhiteSpace(options.WindowClassName))
            return false;

        if (!string.Equals(DialogNative.GetClassName(hwnd), options.WindowClassName, StringComparison.OrdinalIgnoreCase))
            return false;

        var windowTitle = DialogNative.GetWindowText(hwnd);
        if (string.IsNullOrWhiteSpace(windowTitle))
            return false;

        if (!options.DialogTitleKeywords.Any(k => windowTitle.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return false;

        title = windowTitle;
        return true;
    }

    private static bool TryClickPreferredButton(
        IntPtr dialogHwnd,
        StartupDialogOptions options,
        HashSet<IntPtr> clickedButtons)
    {
        var bestButton = IntPtr.Zero;
        var bestScore = int.MaxValue;

        foreach (var button in EnumerateChildButtons(dialogHwnd))
        {
            if (clickedButtons.Contains(button))
                continue;

            var score = GetButtonScore(button, options);
            if (!score.HasValue)
                continue;
            if (score.Value >= bestScore)
                continue;
            bestScore = score.Value;
            bestButton = button;
        }

        if (bestButton == IntPtr.Zero)
            return false;

        if (!DialogNative.TrySendMessageTimeout(
                bestButton, DialogNative.BmClick, IntPtr.Zero, IntPtr.Zero, options.ClickTimeout))
            return false;

        clickedButtons.Add(bestButton);
        return true;
    }

    private static int? GetButtonScore(IntPtr buttonHwnd, StartupDialogOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ButtonClassName))
            return null;

        if (!string.Equals(DialogNative.GetClassName(buttonHwnd), options.ButtonClassName, StringComparison.OrdinalIgnoreCase))
            return null;

        var text = DialogNative.GetWindowText(buttonHwnd);
        if (string.IsNullOrWhiteSpace(text)
            || options.BlockedButtonKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            return null;

        var exactMatchIndex = IndexOfExactMatch(text, options.PreferredButtonKeywords);
        if (exactMatchIndex >= 0)
            return exactMatchIndex;

        var containsMatchIndex = IndexOfContainsMatch(text, options.PreferredButtonKeywords);
        if (containsMatchIndex >= 0)
            return containsMatchIndex + options.PreferredButtonKeywords.Count;

        return null;
    }

    private static int IndexOfExactMatch(string text, IReadOnlyList<string> keywords)
    {
        for (var i = 0; i < keywords.Count; i++)
        {
            if (string.Equals(text, keywords[i], StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static int IndexOfContainsMatch(string text, IReadOnlyList<string> keywords)
    {
        for (var i = 0; i < keywords.Count; i++)
        {
            if (text.Contains(keywords[i], StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static List<IntPtr> EnumerateTopLevelWindowsForProcess(int processId)
    {
        var windows = new List<IntPtr>();
        DialogNative.EnumWindows((hWnd, _) =>
        {
            DialogNative.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid != processId || !DialogNative.IsWindowVisible(hWnd))
                return true;

            windows.Add(hWnd);
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static List<IntPtr> EnumerateChildButtons(IntPtr dialogHwnd)
    {
        var buttons = new List<IntPtr>();
        DialogNative.EnumChildWindows(dialogHwnd, (childHwnd, _) =>
        {
            buttons.Add(childHwnd);
            return true;
        }, IntPtr.Zero);

        return buttons;
    }
}
