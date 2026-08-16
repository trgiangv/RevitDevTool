namespace DevTools.Hosting;

public sealed class StartupDialogResolverOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Timeout for a single BM_CLICK before treating it as failed and retrying next poll.</summary>
    public TimeSpan ClickTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public IReadOnlyList<string> DialogTitleKeywords { get; init; } = [];

    public IReadOnlyList<string> PreferredButtonKeywords { get; init; } = [];

    public IReadOnlyList<string> BlockedButtonKeywords { get; init; } = [];

    public string WindowClassName { get; init; } = "";

    public string ButtonClassName { get; init; } = "";
}

public enum DialogResolution
{
    ClickedPreferredButton,
    Unresolved
}

public sealed record DialogEvent(DialogResolution Resolution);

public sealed record StartupDialogResolverResult(
    bool TimedOut,
    IReadOnlyList<DialogEvent> Events)
{
    public bool HasUnresolvedDialogs => Events.Any(e => e.Resolution == DialogResolution.Unresolved);
}

/// <summary>
/// Polls a process's top-level windows and clicks a preferred button.
/// Catalogs and window/button class names come from the host spec, not this engine.
/// Caller cancellation is the only stop valve — there is no self-timeout.
/// </summary>
public static class StartupDialogResolver
{
    public static async Task<StartupDialogResolverResult> RunAsync(
        int processId,
        StartupDialogResolverOptions options,
        CancellationToken cancellationToken)
    {
        var clickedButtons = new HashSet<IntPtr>();
        var events = new List<DialogEvent>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ScanAndHandleDialogs(processId, options, clickedButtons, events);
                await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Caller timeout / dispose is the valve.
        }

        AppendUnresolvedForRemainingDialogs(processId, options, events);
        return new StartupDialogResolverResult(TimedOut: false, events);
    }

    private static void ScanAndHandleDialogs(
        int processId,
        StartupDialogResolverOptions options,
        HashSet<IntPtr> clickedButtons,
        List<DialogEvent> events)
    {
        foreach (var hwnd in EnumerateTopLevelWindowsForProcess(processId))
            HandleDialogWindow(hwnd, options, clickedButtons, events);
    }

    private static void HandleDialogWindow(
        IntPtr hwnd,
        StartupDialogResolverOptions options,
        HashSet<IntPtr> clickedButtons,
        List<DialogEvent> events)
    {
        if (!IsTargetDialog(hwnd, options, out _))
            return;

        if (!TryClickPreferredButton(hwnd, options, clickedButtons, out _))
            return;

        events.Add(new DialogEvent(DialogResolution.ClickedPreferredButton));
    }

    private static void AppendUnresolvedForRemainingDialogs(
        int processId,
        StartupDialogResolverOptions options,
        List<DialogEvent> events)
    {
        foreach (var hwnd in EnumerateTopLevelWindowsForProcess(processId))
        {
            if (!IsTargetDialog(hwnd, options, out _))
                continue;

            events.Add(new DialogEvent(DialogResolution.Unresolved));
        }
    }

    private static bool IsTargetDialog(IntPtr hwnd, StartupDialogResolverOptions options, out string title)
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
        StartupDialogResolverOptions options,
        HashSet<IntPtr> clickedButtons,
        out string? clickedText)
    {
        clickedText = null;
        var bestButton = IntPtr.Zero;
        var bestScore = int.MaxValue;
        var bestText = string.Empty;

        foreach (var button in EnumerateChildButtons(dialogHwnd))
        {
            if (clickedButtons.Contains(button))
                continue;

            var (score, text) = GetButtonScore(button, options);
            if (!score.HasValue)
                continue;
            if (score.Value >= bestScore)
                continue;
            bestScore = score.Value;
            bestButton = button;
            bestText = text;
        }

        if (bestButton == IntPtr.Zero)
            return false;

        if (!DialogNative.TrySendMessageTimeout(
                bestButton, DialogNative.BmClick, IntPtr.Zero, IntPtr.Zero, options.ClickTimeout))
            return false;

        clickedButtons.Add(bestButton);
        clickedText = bestText;
        return true;
    }

    private static (int? Score, string Text) GetButtonScore(
        IntPtr buttonHwnd,
        StartupDialogResolverOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ButtonClassName))
            return (null, string.Empty);

        if (!string.Equals(DialogNative.GetClassName(buttonHwnd), options.ButtonClassName, StringComparison.OrdinalIgnoreCase))
            return (null, string.Empty);

        var text = DialogNative.GetWindowText(buttonHwnd);
        if (string.IsNullOrWhiteSpace(text)
            || options.BlockedButtonKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            return (null, string.Empty);

        var exactMatchIndex = IndexOfExactMatch(text, options.PreferredButtonKeywords);
        if (exactMatchIndex >= 0)
            return (exactMatchIndex, text);

        var containsMatchIndex = IndexOfContainsMatch(text, options.PreferredButtonKeywords);
        if (containsMatchIndex >= 0)
            return (containsMatchIndex + options.PreferredButtonKeywords.Count, text);

        return (null, string.Empty);
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
