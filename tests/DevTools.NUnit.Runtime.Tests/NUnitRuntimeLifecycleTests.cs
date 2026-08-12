using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Runtime;
using BlockingRunState = DevTools.NUnit.Runtime.Tests.Fixtures.BlockingRunState;
using CancellationProbeState = DevTools.NUnit.Runtime.Tests.Fixtures.CancellationProbeState;

namespace DevTools.NUnit.Runtime.Tests;

[Collection(nameof(BlockingFixtureCollection))]
public sealed class NUnitRuntimeLifecycleTests
{
    private static readonly TimeSpan LifecycleTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Dispose_StopsActiveRunAndCompletesWithinTimeout()
    {
        DedicatedTestFixturesHarness.ResetBlockingState();
        var session = DedicatedTestFixturesHarness.CreateSession();

        var runTask = Task.Run(() => session.Run(
            new NUnitRunRequest(Guid.NewGuid(), DedicatedTestFixturesHarness.AssemblyPath, DedicatedTestFixturesHarness.BlockingFilter),
            new RecordingEventSink(),
            CancellationToken.None));

        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref BlockingRunState.Entered) == 1, LifecycleTimeout));

        var disposeTask = Task.Run(session.Dispose, TestContext.Current.CancellationToken);

        await runTask.WaitAsync(LifecycleTimeout, TestContext.Current.CancellationToken);
        await disposeTask.WaitAsync(LifecycleTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(1, Volatile.Read(ref BlockingRunState.Entered));
    }

    [Fact]
    public async Task Discover_QueuedDuringDispose_ThrowsObjectDisposedException()
    {
        DedicatedTestFixturesHarness.ResetBlockingState();
        var session = DedicatedTestFixturesHarness.CreateSession();
        using var discoverQueued = new ManualResetEventSlim(false);

        var runTask = Task.Run(() => session.Run(
            new NUnitRunRequest(Guid.NewGuid(), DedicatedTestFixturesHarness.AssemblyPath, DedicatedTestFixturesHarness.BlockingFilter),
            new RecordingEventSink(),
            CancellationToken.None));

        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref BlockingRunState.Entered) == 1, LifecycleTimeout));

        var discoverTask = Task.Run(() =>
        {
            discoverQueued.Set();
            return Assert.Throws<ObjectDisposedException>(() =>
                session.Discover(new NUnitDiscoverRequest(DedicatedTestFixturesHarness.AssemblyPath, null)));
        });

        Assert.True(discoverQueued.Wait(LifecycleTimeout, TestContext.Current.CancellationToken));

        var disposeTask = Task.Run(session.Dispose, TestContext.Current.CancellationToken);

        await runTask.WaitAsync(LifecycleTimeout, TestContext.Current.CancellationToken);
        await disposeTask.WaitAsync(LifecycleTimeout, TestContext.Current.CancellationToken);

        var exception = await discoverTask.WaitAsync(LifecycleTimeout, TestContext.Current.CancellationToken);
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task Run_QueuedDuringDispose_ThrowsObjectDisposedException()
    {
        DedicatedTestFixturesHarness.ResetCancellationProbeState();
        DedicatedTestFixturesHarness.ResetBlockingState();
        var session = DedicatedTestFixturesHarness.CreateSession();
        using var gateQueueReady = new ManualResetEventSlim(false);
        using var runQueued = new ManualResetEventSlim(false);

        var activeRunTask = Task.Run(() => session.Run(
            new NUnitRunRequest(Guid.NewGuid(), DedicatedTestFixturesHarness.AssemblyPath, DedicatedTestFixturesHarness.BlockingFilter),
            new RecordingEventSink(),
            CancellationToken.None));

        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref BlockingRunState.Entered) == 1, LifecycleTimeout));

        var gateQueueTask = Task.Run(() =>
        {
            gateQueueReady.Set();
            Assert.Throws<ObjectDisposedException>(() =>
                session.Discover(new NUnitDiscoverRequest(DedicatedTestFixturesHarness.AssemblyPath, null)));
        }, TestContext.Current.CancellationToken);

        Assert.True(gateQueueReady.Wait(LifecycleTimeout, TestContext.Current.CancellationToken));

        var queuedRunTask = Task.Run(() =>
        {
            runQueued.Set();
            return Assert.Throws<ObjectDisposedException>(() =>
                session.Run(
                    new NUnitRunRequest(Guid.NewGuid(), DedicatedTestFixturesHarness.AssemblyPath, DedicatedTestFixturesHarness.CancellationProbeFilter),
                    new RecordingEventSink(),
                    CancellationToken.None));
        });

        Assert.True(runQueued.Wait(LifecycleTimeout, TestContext.Current.CancellationToken));

        var disposeTask = Task.Run(session.Dispose, TestContext.Current.CancellationToken);

        await activeRunTask.WaitAsync(LifecycleTimeout, TestContext.Current.CancellationToken);
        await disposeTask.WaitAsync(LifecycleTimeout, TestContext.Current.CancellationToken);
        await gateQueueTask.WaitAsync(LifecycleTimeout, TestContext.Current.CancellationToken);

        var exception = await queuedRunTask.WaitAsync(LifecycleTimeout, TestContext.Current.CancellationToken);
        Assert.NotNull(exception);
        Assert.Equal(0, Volatile.Read(ref CancellationProbeState.BodyEntered));
    }

    [Fact]
    public async Task Dispose_IsIdempotentUnderConcurrentCalls()
    {
        var session = DedicatedTestFixturesHarness.CreateSession();
        var exceptions = new List<Exception>();
        var disposeTasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() =>
            {
                try
                {
                    session.Dispose();
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                        exceptions.Add(ex);
                }
            }))
            .ToArray();

        await Task.WhenAll(disposeTasks).WaitAsync(LifecycleTimeout, TestContext.Current.CancellationToken);
        Assert.Empty(exceptions);

        Assert.Throws<ObjectDisposedException>(() =>
            session.Discover(new NUnitDiscoverRequest(DedicatedTestFixturesHarness.AssemblyPath, null)));
    }

    [Fact]
    public void Cancel_AfterDispose_ThrowsObjectDisposedException()
    {
        var session = DedicatedTestFixturesHarness.CreateSession();
        session.Dispose();

        Assert.Throws<ObjectDisposedException>(() => session.Cancel(Guid.NewGuid()));
    }
}
