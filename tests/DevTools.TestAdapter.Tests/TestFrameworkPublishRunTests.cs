using System.Reflection;
using DevTools.NUnit.MTP;
using DevTools.TestAdapter;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Config;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Tests;
using DevTools.Testing.Transport;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;
using SessionUid = Microsoft.Testing.Platform.TestHost.SessionUid;

namespace DevTools.TestAdapter.Tests;

#pragma warning disable TPEXP

[DoNotParallelize]
[TestClass]
public sealed class TestFrameworkPublishRunTests
{
    public TestContext TestContext { get; set; } = null!;

    private static readonly object DiscoveryProviderLock = new();

    [TestMethod]
    public async Task Empty_uid_filter_publishes_nothing_and_does_not_call_transport()
    {
        var transport = new FakeTestRunnerTransport();
        var bus = new CapturingMessageBus();
        await ExecuteRunAsync(
            transport,
            bus,
            new SelectionAwareDiscoverer([]),
            PassThroughRunMapper.Instance,
            new TestNodeUidListFilter([]),
            TestContext.CancellationToken);

        Assert.IsNull(transport.LastRequest);
        Assert.IsEmpty(bus.Nodes);
    }

    [TestMethod]
    public async Task Streamed_case_with_unknown_test_id_publishes_no_node()
    {
        var known = new TestDiscoveredTest("known", "Known", "known");
        var transport = new FakeTestRunnerTransport
        {
            Response = new TestRunResponse(
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                "gen",
                [],
                TestCancellationState.None,
                null,
                null),
            StreamedEvents =
            [
                new TestEvent(
                    Guid.NewGuid(),
                    TestEventKinds.Case,
                    new TestCaseResult("ghost", "Ghost", TestOutcomes.Passed, 1, null, null, null, null, [], []),
                    null,
                    null,
                    TestCancellationState.None),
            ],
        };
        var bus = new CapturingMessageBus();
        await ExecuteRunAsync(
            transport,
            bus,
            new SelectionAwareDiscoverer([known]),
            PassThroughRunMapper.Instance,
            new TestNodeUidListFilter([new TestNodeUid("known")]),
            TestContext.CancellationToken);

        Assert.IsNotNull(transport.LastRequest);
        Assert.IsEmpty(bus.Nodes);
    }

    [TestMethod]
    public async Task Streamed_host_id_is_remapped_onto_discovered_uid()
    {
        const string testhostId =
            "Ns.Box.Bottom_corners_share_min_z(-12.3d,45.6d,-7.8d,34.5d,67.8d,12.3d)";
        const string hostId =
            "Ns.Box.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)";
        var discovered = new TestDiscoveredTest(testhostId, "Bottom_corners", testhostId);
        var host = new TestCaseResult(
            hostId, "Bottom_corners", TestOutcomes.Passed, 1, null, null, null, null, [], [], FullName: hostId);
        var transport = new FakeTestRunnerTransport
        {
            Response = new TestRunResponse(
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                "gen",
                [host],
                TestCancellationState.None,
                null,
                null),
            StreamedEvents =
            [
                new TestEvent(
                    Guid.NewGuid(),
                    TestEventKinds.Case,
                    host,
                    null,
                    null,
                    TestCancellationState.None),
            ],
        };
        var bus = new CapturingMessageBus();
        await ExecuteRunAsync(
            transport,
            bus,
            new SelectionAwareDiscoverer([discovered]),
            new NUnitTestRunMapper(),
            new TestNodeUidListFilter([new TestNodeUid(testhostId)]),
            TestContext.CancellationToken);

        var node = Assert.ContainsSingle(bus.Nodes);
        Assert.AreEqual(testhostId, node.Uid.Value);
    }

    [TestMethod]
    public async Task Streamed_case_is_not_published_again_from_fold()
    {
        var known = new TestDiscoveredTest("known", "Known", "known");
        var passed = new TestCaseResult(
            "known", "Known", TestOutcomes.Passed, 1, null, null, null, null, [], []);
        var transport = new FakeTestRunnerTransport
        {
            Response = new TestRunResponse(
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                "gen",
                [passed],
                TestCancellationState.None,
                null,
                null),
        };
        var bus = new CapturingMessageBus();
        await ExecuteRunAsync(
            transport,
            bus,
            new SelectionAwareDiscoverer([known]),
            PassThroughRunMapper.Instance,
            new TestNodeUidListFilter([new TestNodeUid("known")]),
            TestContext.CancellationToken);

        var node = Assert.ContainsSingle(bus.Nodes);
        Assert.AreEqual("known", node.Uid.Value);
    }

    [TestMethod]
    public async Task Fold_still_publishes_when_the_case_was_not_streamed()
    {
        var known = new TestDiscoveredTest("known", "Known", "known");
        var passed = new TestCaseResult(
            "known", "Known", TestOutcomes.Passed, 1, null, null, null, null, [], []);
        var transport = new FakeTestRunnerTransport
        {
            Response = new TestRunResponse(
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                "gen",
                [passed],
                TestCancellationState.None,
                null,
                null),
            StreamedEvents = [],
        };
        var bus = new CapturingMessageBus();
        await ExecuteRunAsync(
            transport,
            bus,
            new SelectionAwareDiscoverer([known]),
            PassThroughRunMapper.Instance,
            new TestNodeUidListFilter([new TestNodeUid("known")]),
            TestContext.CancellationToken);

        var node = Assert.ContainsSingle(bus.Nodes);
        Assert.AreEqual("known", node.Uid.Value);
    }

    [TestMethod]
    public async Task Overlay_message_lands_on_unreported_cases()
    {
        var known = new TestDiscoveredTest("known", "Known", "known");
        var transport = new FakeTestRunnerTransport
        {
            Response = new TestRunResponse(
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                "gen",
                [],
                TestCancellationState.None,
                null,
                "host overlay"),
        };
        var bus = new CapturingMessageBus();
        await ExecuteRunAsync(
            transport,
            bus,
            new SelectionAwareDiscoverer([known]),
            new UnreportedMapper(),
            new TestNodeUidListFilter([new TestNodeUid("known")]),
            TestContext.CancellationToken);

        var node = Assert.ContainsSingle(bus.Nodes);
        Assert.AreEqual("known", node.Uid.Value);
        var error = node.Properties.Single<ErrorTestNodeStateProperty>();
        var overlay = error.Explanation ?? error.Exception?.Message ?? string.Empty;
        Assert.Contains("host overlay", overlay, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Unconstrained_transport_failure_publishes_a_run_error_node()
    {
        var known = new TestDiscoveredTest("known", "Known", "known");
        var transport = new FakeTestRunnerTransport
        {
            RunException = new InvalidOperationException("runner missing"),
        };
        var bus = new CapturingMessageBus();
        await ExecuteRunAsync(
            transport,
            bus,
            new SelectionAwareDiscoverer([known]),
            PassThroughRunMapper.Instance,
            filter: null,
            TestContext.CancellationToken);

        var node = Assert.ContainsSingle(bus.Nodes);
        Assert.AreEqual("devtools.testadapter.run", node.Uid.Value);
        Assert.IsNotNull(node.Properties.SingleOrDefault<ErrorTestNodeStateProperty>());
    }

    [TestMethod]
    public async Task Cancelled_request_does_not_start_the_transport()
    {
        var transport = new FakeTestRunnerTransport();
        var bus = new CapturingMessageBus();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await ExecuteRunAsync(
            transport,
            bus,
            new SelectionAwareDiscoverer([new TestDiscoveredTest("known", "Known", "known")]),
            PassThroughRunMapper.Instance,
            filter: null,
            cts.Token);

        Assert.IsNull(transport.LastRequest);
        var node = Assert.ContainsSingle(bus.Nodes);
        Assert.AreEqual("devtools.testadapter.run", node.Uid.Value);
    }

    [TestMethod]
    public async Task Cancellation_token_cancels_the_active_transport()
    {
        using var runEntered = new ManualResetEventSlim();
        using var blockRun = new ManualResetEventSlim();
        var transport = new FakeTestRunnerTransport
        {
            RunEntered = runEntered,
            BlockRun = blockRun,
        };
        var bus = new CapturingMessageBus();
        using var cts = new CancellationTokenSource();
        var run = Task.Run(
            () => ExecuteRunAsync(
                transport,
                bus,
                new SelectionAwareDiscoverer([new TestDiscoveredTest("known", "Known", "known")]),
                PassThroughRunMapper.Instance,
                filter: null,
                cts.Token),
            TestContext.CancellationToken);

        Assert.IsTrue(runEntered.Wait(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        cts.Cancel();
        blockRun.Set();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        Assert.IsTrue(transport.Cancelled);
    }

    private static Task ExecuteRunAsync(
        ITestRunnerTransport transport,
        IMessageBus bus,
        ITestDiscoverer discoverer,
        ITestRunMapper mapper,
        ITestExecutionFilter? filter,
        CancellationToken cancellationToken)
    {
        lock (DiscoveryProviderLock)
        {
            var previous = TestingDiscovery.Current;
            TestingDiscovery.Clear();
            TestingDiscovery.Register(discoverer, mapper);
            try
            {
                var framework = new TestFramework(new StubServiceProvider(TestConfiguration()), transport);
                framework.ExecuteRequestAsync(CreateRunContext(bus, filter, cancellationToken)).GetAwaiter().GetResult();
            }
            finally
            {
                TestingDiscovery.Clear();
                if (previous is not null)
                    TestingDiscovery.Register(previous.Discoverer, previous.RunMapper);
            }
        }

        return Task.CompletedTask;
    }

    private static ExecuteRequestContext CreateRunContext(
        IMessageBus bus,
        ITestExecutionFilter? filter,
        CancellationToken cancellationToken)
    {
        var sessionUid = new SessionUid(Guid.NewGuid().ToString("N"));
        var session = (TestSessionContext)Activator.CreateInstance(
            typeof(TestSessionContext),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [sessionUid],
            culture: null)!;
        var request = new RunTestExecutionRequest(session, filter ?? new NopFilter());
        return new ExecuteRequestContext(request, bus, new NopCompletionNotifier(), cancellationToken);
    }

    private static IConfiguration TestConfiguration() =>
        new StubConfiguration(new Dictionary<string, string?>
        {
            [TestConfig.Keys.Configuration(TestConfig.Keys.HostName)] = "Revit",
            [TestConfig.Keys.Configuration(TestConfig.Keys.HostVersion)] = "2026",
            [TestConfig.Keys.Configuration(TestConfig.Keys.PerTestTimeoutSeconds)] = "60",
            [TestConfig.Keys.Configuration(TestConfig.Keys.LaunchTimeoutSeconds)] = "180",
            [TestConfig.Keys.Configuration(TestConfig.Keys.FrameworkId)] = "nunit",
            [TestConfig.Keys.Configuration(TestConfig.Keys.RunnerPath)] = @"C:\Runner.exe",
        });

    private sealed class StubServiceProvider(IConfiguration configuration) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IConfiguration) ? configuration : null;
    }

    private sealed class CapturingMessageBus : IMessageBus
    {
        public List<TestNode> Nodes { get; } = [];

        public Task PublishAsync(IDataProducer dataProducer, IData data)
        {
            if (data is TestNodeUpdateMessage update)
                Nodes.Add(update.TestNode);
            return Task.CompletedTask;
        }
    }

    private sealed class NopCompletionNotifier : IExecuteRequestCompletionNotifier
    {
        public void Complete()
        {
        }
    }

    private sealed class SelectionAwareDiscoverer(IReadOnlyList<TestDiscoveredTest> tests) : ITestDiscoverer
    {
        public IReadOnlyList<TestDiscoveredTest> Discover(string assemblyPath, TestSelection selection)
        {
            if (selection.Kind == TestSelectionKind.TestIds && selection.TestIds.Count == 0)
                return [];

            if (selection.Kind != TestSelectionKind.TestIds)
                return tests;

            var ids = selection.TestIds.ToHashSet(StringComparer.Ordinal);
            return tests.Where(test => ids.Contains(test.TestId)).ToList();
        }
    }

    private sealed class UnreportedMapper : ITestRunMapper
    {
        public TestSelection ToRunSelection(
            TestSelection requested,
            IReadOnlyList<TestDiscoveredTest> discovered) =>
            requested;

        public IReadOnlyList<TestCaseResult> FoldResults(
            TestSelection requested,
            IReadOnlyList<TestDiscoveredTest> discovered,
            IReadOnlyList<TestCaseResult> hostResults) =>
            hostResults;

        public IReadOnlyList<TestCaseResult> ResultsForUnreported(
            TestSelection requested,
            IReadOnlyList<TestDiscoveredTest> discovered,
            IReadOnlyList<TestCaseResult> hostResults)
        {
            var reported = hostResults.Select(result => result.TestId).ToHashSet(StringComparer.Ordinal);
            return discovered
                .Where(test => !reported.Contains(test.TestId))
                .Select(test => new TestCaseResult(
                    test.TestId,
                    test.DisplayName,
                    TestOutcomes.Error,
                    0,
                    "unreported",
                    null,
                    null,
                    test.Source,
                    [],
                    [],
                    FullName: test.FullName))
                .ToList();
        }
    }
}
