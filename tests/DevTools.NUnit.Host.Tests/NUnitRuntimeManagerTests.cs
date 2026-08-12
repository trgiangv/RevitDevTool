using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Results;
using DevTools.NUnit.Core.Runtime;
using DevTools.NUnit.Host.Loading;
using DevTools.NUnit.Host.Tests.Loading;
using DevTools.NUnit.Host.Tests.TestSupport;

namespace DevTools.NUnit.Host.Tests;

public sealed class NUnitRuntimeManagerTests
{
    [Fact]
    public void Discover_uses_fake_factory_and_returns_protocol_v2_fields()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Root, "discover");
        var manager = CreateManagerWithFakeFactory(workspace.GenerationsRoot, out var factory);

        var response = manager.Discover(new NUnitDiscoverRequest(testAssembly, null));

        Assert.Single(factory.CreatedManifests);
        Assert.Equal(factory.CreatedManifests[0].GenerationId, response.GenerationId);
        Assert.NotEmpty(response.Cases);
    }

    [Fact]
    public void Run_publishes_progress_maps_case_finished_and_avoids_duplicate_terminal_events()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Root, "run-progress");
        var factory = new FakeNUnitRuntimeSessionFactory();
        var manager = CreateManager(factory, workspace.GenerationsRoot);

        var published = new List<NUnitProgressEvent>();
        var runId = Guid.NewGuid();
        var response = manager.Run(
            new NUnitRunRequest(runId, testAssembly, null),
            published.Add,
            TestContext.Current.CancellationToken);

        Assert.Single(published);
        Assert.Equal(runId, published[0].RunId);
        Assert.Equal(NUnitOutcomes.Passed, published[0].Case.Outcome);
        Assert.Equal(response.Cases[0].Id, published[0].Case.Id);
    }

    [Fact]
    public void Run_publishes_the_same_case_once_for_each_distinct_run()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Root, "repeat-progress");
        var manager = CreateManager(new FakeNUnitRuntimeSessionFactory(), workspace.GenerationsRoot);
        var published = new List<NUnitProgressEvent>();

        manager.Run(new NUnitRunRequest(Guid.NewGuid(), testAssembly, null), published.Add, TestContext.Current.CancellationToken);
        manager.Run(new NUnitRunRequest(Guid.NewGuid(), testAssembly, null), published.Add, TestContext.Current.CancellationToken);

        Assert.Equal(2, published.Count);
        Assert.NotEqual(published[0].RunId, published[1].RunId);
        Assert.Equal(published[0].Case.Id, published[1].Case.Id);
    }

    [Fact]
    public async Task Cancel_reaches_active_session_without_waiting_on_operation_lock()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Root, "cancel");
        var factory = new FakeNUnitRuntimeSessionFactory();
        FakeNUnitRuntimeSession? blockingSession = null;
        var runStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRun = new ManualResetEventSlim(false);

        factory.CreateImpl = generation =>
        {
            blockingSession = new FakeNUnitRuntimeSession(generation.GenerationId, generation.ShadowAssemblyPath)
            {
                RunImpl = (request, sink) =>
                {
                    runStarted.TrySetResult();
                    if (!releaseRun.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("Timed out waiting for cancel release.");

                    var outcome = blockingSession!.CancelRequested
                        ? NUnitOutcomes.Cancelled
                        : NUnitOutcomes.Passed;

                    var result = new NUnitCaseResult(
                        "fake.test#0",
                        "Fake_Test",
                        outcome,
                        1.0,
                        outcome == NUnitOutcomes.Cancelled ? "Cancelled" : null,
                        null,
                        null);

                    return new NUnitRunResponse(
                        request.RunId,
                        new NUnitRunSummary(
                            outcome == NUnitOutcomes.Passed ? 1 : 0,
                            0,
                            0,
                            0,
                            0,
                            outcome == NUnitOutcomes.Cancelled ? 1 : 0),
                        [result],
                        generation.GenerationId);
                },
            };
            return blockingSession;
        };

        var manager = CreateManager(factory, workspace.GenerationsRoot);
        var runId = Guid.NewGuid();
        var runTask = Task.Run(() =>
            manager.Run(new NUnitRunRequest(runId, testAssembly, null), _ => { }, TestContext.Current.CancellationToken));

        await runStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        manager.Cancel(runId);
        Assert.True(blockingSession!.CancelRequested);

        releaseRun.Set();
        var response = await runTask.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, response.Summary.Cancelled);
    }

    [Fact]
    public async Task CancellationToken_reaches_active_session_like_explicit_Cancel()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Root, "cancel-token");
        var factory = new FakeNUnitRuntimeSessionFactory();
        FakeNUnitRuntimeSession? blockingSession = null;
        var runStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRun = new ManualResetEventSlim(false);

        factory.CreateImpl = generation =>
        {
            blockingSession = new FakeNUnitRuntimeSession(generation.GenerationId, generation.ShadowAssemblyPath)
            {
                RunImpl = (request, sink) =>
                {
                    runStarted.TrySetResult();
                    if (!releaseRun.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("Timed out waiting for cancel-token release.");

                    var outcome = blockingSession!.CancelRequested
                        ? NUnitOutcomes.Cancelled
                        : NUnitOutcomes.Passed;

                    var result = new NUnitCaseResult(
                        "fake.test#0",
                        "Fake_Test",
                        outcome,
                        1.0,
                        outcome == NUnitOutcomes.Cancelled ? "Cancelled" : null,
                        null,
                        null);

                    return new NUnitRunResponse(
                        request.RunId,
                        new NUnitRunSummary(
                            outcome == NUnitOutcomes.Passed ? 1 : 0,
                            0,
                            0,
                            0,
                            0,
                            outcome == NUnitOutcomes.Cancelled ? 1 : 0),
                        [result],
                        generation.GenerationId);
                },
            };
            return blockingSession;
        };

        var manager = CreateManager(factory, workspace.GenerationsRoot);
        using var cts = new CancellationTokenSource();
        var runTask = Task.Run(() =>
            manager.Run(new NUnitRunRequest(Guid.NewGuid(), testAssembly, null), _ => { }, cts.Token));

        await runStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        Assert.True(blockingSession!.CancelRequested);

        releaseRun.Set();
        var response = await runTask.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, response.Summary.Cancelled);
    }

    [Fact]
    public async Task Second_run_waits_behind_first_operation_without_concurrent_execution()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Root, "serialize");
        var factory = new FakeNUnitRuntimeSessionFactory();
        var concurrentRuns = 0;
        var runInvocations = 0;
        var firstRunEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRunEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        factory.CreateImpl = generation =>
        {
            var session = new FakeNUnitRuntimeSession(generation.GenerationId, generation.ShadowAssemblyPath)
            {
                RunImpl = (request, sink) =>
                {
                    var invocation = Interlocked.Increment(ref runInvocations);
                    var active = Interlocked.Increment(ref concurrentRuns);
                    if (active != 1)
                        throw new InvalidOperationException("Concurrent NUnit runs are not allowed.");

                    if (invocation == 1)
                        firstRunEntered.TrySetResult();
                    else
                        secondRunEntered.TrySetResult();

                    if (invocation == 1
                        && !releaseFirstRun.Task.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("Timed out waiting to release the first NUnit run.");

                    var result = new NUnitCaseResult(
                        "fake.test#0",
                        "Fake_Test",
                        NUnitOutcomes.Passed,
                        1.0,
                        null,
                        null,
                        null);

                    sink.Publish(new NUnitRuntimeEvent(request.RunId, "case.finished", result, null, null));
                    Interlocked.Decrement(ref concurrentRuns);

                    return new NUnitRunResponse(
                        request.RunId,
                        new NUnitRunSummary(1, 0, 0, 0, 0, 0),
                        [result],
                        generation.GenerationId);
                },
            };
            return session;
        };

        var manager = CreateManager(factory, workspace.GenerationsRoot);
        var firstRun = Task.Run(() =>
            manager.Run(new NUnitRunRequest(Guid.NewGuid(), testAssembly, null), _ => { }, TestContext.Current.CancellationToken));
        await firstRunEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        var secondRun = Task.Run(() =>
            manager.Run(new NUnitRunRequest(Guid.NewGuid(), testAssembly, null), _ => { }, TestContext.Current.CancellationToken));

        var secondEnteredBeforeRelease = false;
        try
        {
            await secondRunEntered.Task.WaitAsync(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
            secondEnteredBeforeRelease = true;
        }
        catch (TimeoutException)
        {
            // Expected while the first run still owns the operation lock.
        }

        Assert.False(secondEnteredBeforeRelease);
        Assert.False(secondRun.IsCompleted);

        releaseFirstRun.TrySetResult();
        await firstRun.WaitAsync(TestContext.Current.CancellationToken);
        await secondRunEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await secondRun.WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Reuses_session_for_same_generation_and_disposes_obsolete_generation_on_change()
    {
        using var workspace = new TempWorkspace();
        var generationOneAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Root, "reuse-one");
        var generationTwoAssembly = NUnitGenerationTestEnvironment.CreateGenerationTwoAssembly(workspace.Root, "reuse-two");
        var factory = new FakeNUnitRuntimeSessionFactory();
        var disposedGenerationIds = new List<string>();

        factory.CreateImpl = generation =>
        {
            var session = new FakeNUnitRuntimeSession(generation.GenerationId, generation.ShadowAssemblyPath);
            return new TrackingRuntimeSession(session, () => disposedGenerationIds.Add(generation.GenerationId));
        };

        var manager = CreateManager(factory, workspace.GenerationsRoot);

        manager.Discover(new NUnitDiscoverRequest(generationOneAssembly, null));
        manager.Discover(new NUnitDiscoverRequest(generationOneAssembly, null));
        Assert.Single(factory.CreatedManifests);

        manager.Discover(new NUnitDiscoverRequest(generationTwoAssembly, null));
        Assert.Equal(2, factory.CreatedManifests.Count);
        Assert.Contains(factory.CreatedManifests[0].GenerationId, disposedGenerationIds, StringComparer.Ordinal);
        Assert.DoesNotContain(factory.CreatedManifests[1].GenerationId, disposedGenerationIds, StringComparer.Ordinal);
    }

    [Fact]
    public void Rebuilt_fixture_generations_produce_distinct_ids_and_observed_marker_values()
    {
        using var workspace = new TempWorkspace();
        var generationOneAssembly = NUnitGenerationTestEnvironment.BuildFixtureGeneration(
            workspace.Root,
            "fixture-one",
            "generation-one");
        var generationTwoAssembly = NUnitGenerationTestEnvironment.BuildFixtureGeneration(
            workspace.Root,
            "fixture-two",
            "generation-two");
        var manager = CreateManagerWithRealFactory(workspace.GenerationsRoot);

        var generationOneManifest = ModernNUnitRuntimeTestEnvironment.CreateBuilder(workspace.GenerationsRoot)
            .Build(generationOneAssembly);
        var generationTwoManifest = ModernNUnitRuntimeTestEnvironment.CreateBuilder(workspace.GenerationsRoot)
            .Build(generationTwoAssembly);

        var filter = "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.GenerationMarker_IsReported</test></filter>";

        var generationOneRun = manager.Run(
            new NUnitRunRequest(Guid.NewGuid(), generationOneAssembly, filter),
            _ => { },
            TestContext.Current.CancellationToken);

        var generationTwoRun = manager.Run(
            new NUnitRunRequest(Guid.NewGuid(), generationTwoAssembly, filter),
            _ => { },
            TestContext.Current.CancellationToken);

        Assert.Equal(generationOneManifest.GenerationId, generationOneRun.GenerationId);
        Assert.Equal(generationTwoManifest.GenerationId, generationTwoRun.GenerationId);
        Assert.NotEqual(generationOneRun.GenerationId, generationTwoRun.GenerationId);

        Assert.Equal(NUnitOutcomes.Passed, generationOneRun.Cases.Single().Outcome);
        Assert.Equal(NUnitOutcomes.Passed, generationTwoRun.Cases.Single().Outcome);
        Assert.Contains("generation-one", generationOneRun.Cases.Single().Output ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("generation-two", generationTwoRun.Cases.Single().Output ?? string.Empty, StringComparison.Ordinal);
    }

    private static NUnitRuntimeManager CreateManagerWithFakeFactory(
        string generationsRoot,
        out FakeNUnitRuntimeSessionFactory factory)
    {
        factory = new FakeNUnitRuntimeSessionFactory();
        return CreateManager(factory, generationsRoot);
    }

    private static NUnitRuntimeManager CreateManagerWithRealFactory(string generationsRoot) =>
        new(
            ModernNUnitRuntimeTestEnvironment.CreateBuilder(generationsRoot),
            new ModernNUnitRuntimeSessionFactory(),
            new NUnitAssemblyLoader());

    private static NUnitRuntimeManager CreateManager(
        FakeNUnitRuntimeSessionFactory factory,
        string generationsRoot) =>
        new(
            ModernNUnitRuntimeTestEnvironment.CreateBuilder(generationsRoot),
            factory,
            new NUnitAssemblyLoader());

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "DevTools",
                "NUnit",
                "ManagerTests",
                Guid.NewGuid().ToString("N"));
            GenerationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string GenerationsRoot { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
                if (Directory.Exists(GenerationsRoot))
                    Directory.Delete(GenerationsRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for temp workspaces.
            }
        }
    }

    private sealed class TrackingRuntimeSession : INUnitRuntimeSession
    {
        private readonly FakeNUnitRuntimeSession _inner;
        private readonly Action _onDispose;

        internal TrackingRuntimeSession(FakeNUnitRuntimeSession inner, Action onDispose)
        {
            _inner = inner;
            _onDispose = onDispose;
        }

        public string GenerationId => _inner.GenerationId;

        public NUnitDiscoverResponse Discover(NUnitDiscoverRequest request) => _inner.Discover(request);

        public NUnitRunResponse Run(
            NUnitRunRequest request,
            INUnitRuntimeEventSink eventSink,
            CancellationToken cancellationToken) =>
            _inner.Run(request, eventSink, cancellationToken);

        public void Cancel(Guid runId) => _inner.Cancel(runId);

        public void Dispose()
        {
            _onDispose();
            _inner.Dispose();
        }
    }
}
