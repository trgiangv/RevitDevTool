using System.Diagnostics;
using DevTools.Hosting;

namespace DevTools.Hosting.Tests;

[TestClass]
public sealed class StartupDialogResolverTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Start_returns_null_for_missing_spec_or_cancelled_token()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.IsNull(StartupDialogResolver.Start(null, 1, TestContext.CancellationToken));
        Assert.IsNull(StartupDialogResolver.Start(new StubDialogSpec(), 1, cts.Token));
    }

    [TestMethod]
    public async Task RunAsync_returns_empty_result_when_cancelled_immediately()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await StartupDialogResolver.RunAsync(
            Process.GetCurrentProcess().Id,
            new StartupDialogOptions { PollInterval = TimeSpan.FromMilliseconds(10) },
            cts.Token);

        Assert.IsTrue(result.Resolved);
        Assert.AreEqual(0, result.ClickCount);
        Assert.IsEmpty(result.Clicked);
        Assert.IsEmpty(result.Remaining);
    }

    [TestMethod]
    public async Task Session_dispose_cancels_background_poll()
    {
        using var session = StartupDialogResolver.Start(
            new StubDialogSpec(),
            Process.GetCurrentProcess().Id,
            TestContext.CancellationToken);
        Assert.IsNotNull(session);

        session!.Dispose();
        var completed = await session.Completion.WaitAsync(TimeSpan.FromSeconds(2), TestContext.CancellationToken);
        Assert.IsNotNull(completed);
    }

    [TestMethod]
    public async Task Session_TryGetResultAsync_returns_null_when_not_complete()
    {
        using var session = StartupDialogResolver.Start(
            new StubDialogSpec(),
            Process.GetCurrentProcess().Id,
            TestContext.CancellationToken);
        Assert.IsNotNull(session);

        var pending = await session!.TryGetResultAsync(TimeSpan.FromMilliseconds(20));
        Assert.IsNull(pending);

        session.Dispose();
        var completed = await session.TryGetResultAsync(TimeSpan.FromSeconds(2));
        Assert.IsNotNull(completed);
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
