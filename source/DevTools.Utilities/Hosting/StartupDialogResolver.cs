using Microsoft.Extensions.Logging;

namespace DevTools.Utilities.Hosting;

public sealed class StartupDialogResolverOptions
{
    public TimeSpan PollInterval { get; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Timeout for a single BM_CLICK before treating it as failed and retrying next poll.</summary>
    public TimeSpan ClickTimeout { get; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Autodesk add-in security dialogs only (e.g. "Security - Unsigned Add-In").
    /// Intentionally narrow — we are not dismissing sign-in, journal, or license prompts.
    /// </summary>
    public IReadOnlyList<string> DialogTitleKeywords { get; } =
    [
        "unsigned add-in",
        "questionable add-in",
        "unsigned executable file"
    ];

    /// <summary>Only "Always Load" — persists trust so subsequent launches stay unattended.</summary>
    public IReadOnlyList<string> PreferredButtonKeywords { get; } =
    [
        "always load"
    ];

    public IReadOnlyList<string> BlockedButtonKeywords { get; } =
    [
        "do not load",
        "load once",
        "cancel",
        "no"
    ];
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
/// Polls a process's top-level windows for Autodesk add-in security dialogs and clicks
/// "Always Load" so unattended launches are not blocked. Does not dismiss unrelated
/// prompts (sign-in, journal, license).
/// </summary>
public static class StartupDialogResolver
{
    public static async Task<StartupDialogResolverResult> RunAsync(
        int processId,
        StartupDialogResolverOptions options,
        TimeSpan duration,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + duration;
        var clickedButtons = new HashSet<IntPtr>();
        var events = new List<DialogEvent>();

        bool timedOut;
        while (true)
        {
            ScanAndHandleDialogs(processId, options, clickedButtons, events, logger);

            if (cancellationToken.IsCancellationRequested)
            {
                timedOut = false;
                break;
            }

            if (DateTime.UtcNow >= deadline)
            {
                timedOut = true;
                break;
            }

            await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
        }

        AppendUnresolvedForRemainingDialogs(processId, options, events, logger);
        return new StartupDialogResolverResult(timedOut, events);
    }

    private static void ScanAndHandleDialogs(
        int processId,
        StartupDialogResolverOptions options,
        HashSet<IntPtr> clickedButtons,
        List<DialogEvent> events,
        ILogger? logger)
    {
        foreach (var hwnd in EnumerateTopLevelWindowsForProcess(processId))
            HandleDialogWindow(hwnd, options, clickedButtons, events, logger);
    }

    private static void HandleDialogWindow(
        IntPtr hwnd,
        StartupDialogResolverOptions options,
        HashSet<IntPtr> clickedButtons,
        List<DialogEvent> events,
        ILogger? logger)
    {
        if (!IsTargetDialog(hwnd, options.DialogTitleKeywords, out var title))
            return;

        if (!TryClickPreferredButton(hwnd, options.PreferredButtonKeywords, options.BlockedButtonKeywords,
                options.ClickTimeout, clickedButtons, out var clickedText))
            return;

        logger?.LogInformation("Dismissed add-in security dialog {Title} via {Button}", title, clickedText);
        events.Add(new DialogEvent(DialogResolution.ClickedPreferredButton));
    }

    private static void AppendUnresolvedForRemainingDialogs(
        int processId,
        StartupDialogResolverOptions options,
        List<DialogEvent> events,
        ILogger? logger)
    {
        foreach (var hwnd in EnumerateTopLevelWindowsForProcess(processId))
        {
            if (!IsTargetDialog(hwnd, options.DialogTitleKeywords, out var title))
                continue;

            logger?.LogWarning("Add-in security dialog still present at resolver end: {Title}", title);
            events.Add(new DialogEvent(DialogResolution.Unresolved));
        }
    }

    private static bool IsTargetDialog(IntPtr hwnd, IReadOnlyList<string> keywords, out string title)
    {
        title = string.Empty;

        if (!string.Equals(Win32Utils.GetClassName(hwnd), Win32Utils.DialogClass, StringComparison.OrdinalIgnoreCase))
            return false;

        var windowTitle = Win32Utils.GetWindowText(hwnd);
        if (string.IsNullOrWhiteSpace(windowTitle))
            return false;

        if (!keywords.Any(k => windowTitle.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return false;

        title = windowTitle;
        return true;
    }

    private static bool TryClickPreferredButton(
        IntPtr dialogHwnd,
        IReadOnlyList<string> preferredKeywords,
        IReadOnlyList<string> blockedKeywords,
        TimeSpan clickTimeout,
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

            var (score, text) = GetButtonScore(button, preferredKeywords, blockedKeywords);
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

        if (!Win32Utils.TrySendMessageTimeout(bestButton, Win32Utils.BmClick, IntPtr.Zero, IntPtr.Zero, clickTimeout))
            return false;

        clickedButtons.Add(bestButton);
        clickedText = bestText;
        return true;
    }

    private static (int? Score, string Text) GetButtonScore(
        IntPtr buttonHwnd,
        IReadOnlyList<string> preferredKeywords,
        IReadOnlyList<string> blockedKeywords)
    {
        if (!string.Equals(Win32Utils.GetClassName(buttonHwnd), Win32Utils.ButtonClass, StringComparison.OrdinalIgnoreCase))
            return (null, string.Empty);

        var text = Win32Utils.GetWindowText(buttonHwnd);
        if (string.IsNullOrWhiteSpace(text) || blockedKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            return (null, string.Empty);

        var exactMatchIndex = IndexOfExactMatch(text, preferredKeywords);
        if (exactMatchIndex >= 0)
            return (exactMatchIndex, text);

        var containsMatchIndex = IndexOfContainsMatch(text, preferredKeywords);
        if (containsMatchIndex >= 0)
            return (containsMatchIndex + preferredKeywords.Count, text);

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
        Win32Utils.EnumWindows((hWnd, _) =>
        {
            Win32Utils.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid != processId || !Win32Utils.IsWindowVisible(hWnd))
                return true;

            windows.Add(hWnd);
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static List<IntPtr> EnumerateChildButtons(IntPtr dialogHwnd)
    {
        var buttons = new List<IntPtr>();
        Win32Utils.EnumChildWindows(dialogHwnd, (childHwnd, _) =>
        {
            buttons.Add(childHwnd);
            return true;
        }, IntPtr.Zero);

        return buttons;
    }
}
