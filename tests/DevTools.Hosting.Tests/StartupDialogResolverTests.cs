using System.Diagnostics;
using DevTools.Hosting;

namespace DevTools.Hosting.Tests;

public sealed class StartupDialogResolverTests
{
    [Fact]
    public void Start_returns_null_for_missing_spec_or_cancelled_token()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Null(StartupDialogResolver.Start(null, 1, TestContext.Current.CancellationToken));
        Assert.Null(StartupDialogResolver.Start(new StubDialogSpec(), 1, cts.Token));
    }

    [Fact]
    public async Task RunAsync_returns_empty_result_when_cancelled_immediately()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await StartupDialogResolver.RunAsync(
            Process.GetCurrentProcess().Id,
            new StartupDialogOptions { PollInterval = TimeSpan.FromMilliseconds(10) },
            cts.Token);

        Assert.True(result.Resolved);
        Assert.Equal(0, result.ClickCount);
        Assert.Empty(result.Clicked);
        Assert.Empty(result.Remaining);
    }

    [Fact]
    public async Task Session_dispose_cancels_background_poll()
    {
        using var session = StartupDialogResolver.Start(
            new StubDialogSpec(),
            Process.GetCurrentProcess().Id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(session);

        session!.Dispose();
        var completed = await session.Completion.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.NotNull(completed);
    }

    [Fact]
    public async Task Session_TryGetResultAsync_returns_null_when_not_complete()
    {
        using var session = StartupDialogResolver.Start(
            new StubDialogSpec(),
            Process.GetCurrentProcess().Id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(session);

        var pending = await session!.TryGetResultAsync(TimeSpan.FromMilliseconds(20));
        Assert.Null(pending);

        session.Dispose();
        var completed = await session.TryGetResultAsync(TimeSpan.FromSeconds(2));
        Assert.NotNull(completed);
    }

    private sealed class StubDialogSpec : IHostStartupDialogSpec
    {
        public bool Supports(HostApp hostApp) => true;

        public StartupDialogOptions CreateOptions() => new()
        {
            WindowClassName = "NotARealDialog",
            ButtonClassName = "Button",
            DialogTitleKeywords = ["missing"],
            PreferredButtonKeywords = ["OK"],
            PollInterval = TimeSpan.FromMilliseconds(25),
            ClickTimeout = TimeSpan.FromMilliseconds(50),
        };
    }
}
