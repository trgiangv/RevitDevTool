using DevTools.NUnit.Transport.Contracts;
using DevTools.NUnit.Transport.Results;
using DevTools.NUnit.Runtime;
using BlockingRunState = DevTools.NUnit.Runtime.Tests.Fixtures.BlockingRunState;
using CancellationProbeState = DevTools.NUnit.Runtime.Tests.Fixtures.CancellationProbeState;
using PartialCancelState = DevTools.NUnit.Runtime.Tests.Fixtures.PartialCancelState;

namespace DevTools.NUnit.Runtime.Tests;

[CollectionDefinition(nameof(BlockingFixtureCollection), DisableParallelization = true)]
public sealed class BlockingFixtureCollection;

[Collection(nameof(BlockingFixtureCollection))]
public sealed class NUnitRuntimeCancellationTests
{
    private static readonly TimeSpan CancellationTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Cancel_StopsBlockingRunWithinTimeout()
    {
        DedicatedTestFixturesHarness.ResetBlockingState();
        using var session = DedicatedTestFixturesHarness.CreateSession();
        var sink = new RecordingEventSink();
        var runId = Guid.NewGuid();

        var runTask = Task.Run(() => session.Run(
            new NUnitRunRequest(runId, DedicatedTestFixturesHarness.AssemblyPath, DedicatedTestFixturesHarness.BlockingFilter),
            sink,
            CancellationToken.None));

        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref BlockingRunState.Entered) == 1, CancellationTimeout));

        session.Cancel(runId);

        var response = await runTask.WaitAsync(CancellationTimeout, TestContext.Current.CancellationToken);
        var blocked = Assert.Single(response.Cases);
        Assert.Equal(NUnitOutcomes.Cancelled, blocked.Outcome);
        Assert.Equal(1, response.Summary.Cancelled);
    }

    [Fact]
    public async Task CancellationToken_StopsBlockingRunWithinTimeout()
    {
        DedicatedTestFixturesHarness.ResetBlockingState();
        using var session = DedicatedTestFixturesHarness.CreateSession();
        var sink = new RecordingEventSink();
        using var cts = new CancellationTokenSource();

        var runTask = Task.Run(() => session.Run(
            new NUnitRunRequest(Guid.NewGuid(), DedicatedTestFixturesHarness.AssemblyPath, DedicatedTestFixturesHarness.BlockingFilter),
            sink,
            cts.Token));

        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref BlockingRunState.Entered) == 1, CancellationTimeout));

        await cts.CancelAsync();

        var response = await runTask.WaitAsync(CancellationTimeout, TestContext.Current.CancellationToken);
        var blocked = Assert.Single(response.Cases);
        Assert.Equal(NUnitOutcomes.Cancelled, blocked.Outcome);
        Assert.Equal(1, response.Summary.Cancelled);
    }

    [Fact]
    public void AlreadyCancelledToken_DoesNotExecuteFixtureAndCompletesImmediately()
    {
        DedicatedTestFixturesHarness.ResetCancellationProbeState();
        using var session = DedicatedTestFixturesHarness.CreateSession();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var response = session.Run(
            new NUnitRunRequest(Guid.NewGuid(), DedicatedTestFixturesHarness.AssemblyPath, DedicatedTestFixturesHarness.CancellationProbeFilter),
            new RecordingEventSink(),
            cts.Token);

        Assert.Empty(response.Cases);
        Assert.Equal(0, response.Summary.Cancelled);
        Assert.Equal(0, Volatile.Read(ref CancellationProbeState.BodyEntered));
    }

    [Fact]
    public async Task Cancel_RequestedImmediatelyAfterRunAccepted_SkipsExecutionWhenStopWins()
    {
        DedicatedTestFixturesHarness.ResetBlockingState();
        using var session = DedicatedTestFixturesHarness.CreateSession();
        var runId = Guid.NewGuid();

        var runTask = Task.Run(() => session.Run(
            new NUnitRunRequest(runId, DedicatedTestFixturesHarness.AssemblyPath, DedicatedTestFixturesHarness.BlockingFilter),
            new RecordingEventSink(),
            CancellationToken.None));

        session.Cancel(runId);

        var response = await runTask.WaitAsync(CancellationTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(0, Volatile.Read(ref BlockingRunState.Entered));
        Assert.Empty(response.Cases);
        Assert.Equal(0, response.Summary.Cancelled);
    }

    [Fact]
    public async Task PartialCancel_PreservesCompletedCaseAndCancelledBlockingCase()
    {
        DedicatedTestFixturesHarness.ResetPartialCancelState();
        using var session = DedicatedTestFixturesHarness.CreateSession();
        var sink = new RecordingEventSink();
        var runId = Guid.NewGuid();

        var runTask = Task.Run(() => session.Run(
            new NUnitRunRequest(runId, DedicatedTestFixturesHarness.AssemblyPath, DedicatedTestFixturesHarness.PartialCancelFilter),
            sink,
            CancellationToken.None));

        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref PartialCancelState.SecondEntered) == 1, CancellationTimeout));
        Assert.Equal(1, Volatile.Read(ref PartialCancelState.FirstCompleted));

        session.Cancel(runId);

        var response = await runTask.WaitAsync(CancellationTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(2, response.Cases.Count);
        Assert.Equal(1, response.Summary.Passed);
        Assert.Equal(1, response.Summary.Cancelled);

        var passed = Assert.Single(response.Cases, testCase => testCase.Outcome == NUnitOutcomes.Passed);
        var cancelled = Assert.Single(response.Cases, testCase => testCase.Outcome == NUnitOutcomes.Cancelled);
        Assert.Contains("CompletesFirst", passed.Id, StringComparison.Ordinal);
        Assert.Contains("BlocksSecond", cancelled.Id, StringComparison.Ordinal);
        Assert.NotEqual(passed.Id, cancelled.Id);
    }
}
