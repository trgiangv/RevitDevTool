namespace RevitDevTool.McpServer.Tools.Utils;

internal sealed class StartupDialogResolverOptions
{
    public TimeSpan PollInterval { get; } = TimeSpan.FromMilliseconds(500);

    public const int MaxNoButtonRetriesPerWindow = 3;

    public IReadOnlyList<string> DialogTitleKeywords { get; } =
    [
        "autodesk",
        "revit",
        "load",
        "security",
        "warning",
        "add-in",
        "addin",
        "questionable add-in",
        "unsigned add-in"
    ];

    public IReadOnlyList<string> PreferredButtonKeywords { get; } =
    [
        "always load",
        "load once",
        "load",
        "ok",
        "yes",
        "accept",
        "close",
        "continue",
        "skip"
    ];

    public IReadOnlyList<string> BlockedButtonKeywords { get; } =
    [
        "do not load",
        "cancel",
        "no"
    ];
}

internal static class StartupDialogResolver
{
    public static async Task RunAsync(
        int processId,
        StartupDialogResolverOptions options,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + duration;
        var retries = new Dictionary<nint, int>();

        while (CanContinue(deadline, cancellationToken))
        {
            ScanAndHandleDialogs(processId, options, retries);
            await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool CanContinue(DateTime deadline, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested && DateTime.UtcNow < deadline;
    }

    private static void ScanAndHandleDialogs(
        int processId,
        StartupDialogResolverOptions options,
        Dictionary<nint, int> retries)
    {
        foreach (var hwnd in EnumerateTopLevelWindowsForProcess(processId))
            HandleDialogWindow(hwnd, options, retries);
    }

    private static void HandleDialogWindow(
        nint hwnd,
        StartupDialogResolverOptions options,
        Dictionary<nint, int> retries)
    {
        if (!LooksLikeTargetDialog(hwnd, options.DialogTitleKeywords))
            return;

        if (TryClickPreferredButton(hwnd, options.PreferredButtonKeywords, options.BlockedButtonKeywords))
            return;

        IncreaseRetryCount(hwnd, StartupDialogResolverOptions.MaxNoButtonRetriesPerWindow, retries);
    }

    private static void IncreaseRetryCount(
        nint hwnd,
        int maxNoButtonRetriesPerWindow,
        Dictionary<nint, int> retries)
    {
        retries.TryGetValue(hwnd, out var count);
        count++;

        if (count > maxNoButtonRetriesPerWindow)
        {
            retries.Remove(hwnd);
            return;
        }

        retries[hwnd] = count;
    }

    private static bool LooksLikeTargetDialog(nint hwnd, IReadOnlyList<string> keywords)
    {
        if (!string.Equals(NativeMethods.GetClassName(hwnd), "#32770", StringComparison.OrdinalIgnoreCase))
            return false;

        var title = NativeMethods.GetWindowText(hwnd);
        return !string.IsNullOrWhiteSpace(title) && keywords.Any(k => title.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryClickPreferredButton(
        nint dialogHwnd,
        IReadOnlyList<string> preferredKeywords,
        IReadOnlyList<string> blockedKeywords)
    {
        var bestButton = nint.Zero;
        var bestScore = int.MaxValue;
        foreach (var button in EnumerateChildButtons(dialogHwnd))
        {
            var score = GetButtonScore(button, preferredKeywords, blockedKeywords);
            if (!score.HasValue)
                continue;
            if (score.Value >= bestScore)
                continue;
            bestScore = score.Value;
            bestButton = button;
        }

        if (bestButton == nint.Zero)
            return false;

        NativeMethods.SendMessage(bestButton, NativeMethods.BmClick, 0, 0);
        return true;
    }

    private static int? GetButtonScore(
        nint buttonHwnd,
        IReadOnlyList<string> preferredKeywords,
        IReadOnlyList<string> blockedKeywords)
    {
        if (!string.Equals(NativeMethods.GetClassName(buttonHwnd), "button", StringComparison.OrdinalIgnoreCase))
            return null;

        var text = NativeMethods.GetWindowText(buttonHwnd);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (blockedKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            return null;

        var exactMatchIndex = IndexOfExactMatch(text, preferredKeywords);
        if (exactMatchIndex >= 0)
            return exactMatchIndex;

        var containsMatchIndex = IndexOfContainsMatch(text, preferredKeywords);
        if (containsMatchIndex >= 0)
            return containsMatchIndex + preferredKeywords.Count;

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

    private static List<nint> EnumerateTopLevelWindowsForProcess(int processId)
    {
        var windows = new List<nint>();
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid != processId || !NativeMethods.IsWindowVisible(hWnd))
                return true;

            windows.Add(hWnd);
            return true;
        }, nint.Zero);

        return windows;
    }

    private static List<nint> EnumerateChildButtons(nint dialogHwnd)
    {
        var buttons = new List<nint>();
        NativeMethods.EnumChildWindows(dialogHwnd, (childHwnd, _) =>
        {
            buttons.Add(childHwnd);
            return true;
        }, nint.Zero);

        return buttons;
    }
}
