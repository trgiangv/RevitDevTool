using System.Reflection;
using DevTools.TestAdapter;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Config;
using DevTools.Testing.Abstractions.Contracts;
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

[Collection(nameof(AdapterHostTestDiscoveryCollection))]
public sealed class HostTestFrameworkPublishRunTests
{
    private static readonly object DiscoveryProviderLock = new();

    [Fact]
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
            TestContext.Current.CancellationToken);

        Assert.Null(transport.LastRequest);
        Assert.Empty(bus.Nodes);
    }

    [Fact]
    public async Task Streamed_case_with_unknown_test_id_publishes_no_node()
    {
        var known = new TestingDiscoveredTest("known", "Known", "known");
        var transport = new FakeTestRunnerTransport
        {
            Response = new TestingRunResponse(
                Guid.NewGuid(),
                "nunit",
                "gen",
                [],
                TestingCancellationState.None,
                null,
                null),
            StreamedEvents =
            [
                new TestingEvent(
                    Guid.NewGuid(),
                    TestingEventKinds.Case,
                    new TestingCaseResult("ghost", "Ghost", TestingOutcomes.Passed, 1, null, null, null, null, [], []),
                    null,
                    null,
                    TestingCancellationState.None),
            ],
        };
        var bus = new CapturingMessageBus();
        await ExecuteRunAsync(
            transport,
            bus,
            new SelectionAwareDiscoverer([known]),
            PassThroughRunMapper.Instance,
            new TestNodeUidListFilter([new TestNodeUid("known")]),
            TestContext.Current.CancellationToken);

        Assert.NotNull(transport.LastRequest);
        Assert.Empty(bus.Nodes);
    }

    [Fact]
    public async Task Overlay_message_lands_on_unreported_cases()
    {
        var known = new TestingDiscoveredTest("known", "Known", "known");
        var transport = new FakeTestRunnerTransport
        {
            Response = new TestingRunResponse(
                Guid.NewGuid(),
                "nunit",
                "gen",
                [],
                TestingCancellationState.None,
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
            TestContext.Current.CancellationToken);

        var node = Assert.Single(bus.Nodes);
        Assert.Equal("known", node.Uid.Value);
        var error = node.Properties.Single<ErrorTestNodeStateProperty>();
        var overlay = error.Explanation ?? error.Exception?.Message ?? string.Empty;
        Assert.Contains("host overlay", overlay, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unconstrained_transport_failure_publishes_a_run_error_node()
    {
        var known = new TestingDiscoveredTest("known", "Known", "known");
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
            TestContext.Current.CancellationToken);

        var node = Assert.Single(bus.Nodes);
        Assert.Equal("devtools.testadapter.run", node.Uid.Value);
        Assert.NotNull(node.Properties.SingleOrDefault<ErrorTestNodeStateProperty>());
    }

    [Fact]
    public async Task Cancelled_request_does_not_start_the_transport()
    {
        var transport = new FakeTestRunnerTransport();
        var bus = new CapturingMessageBus();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await ExecuteRunAsync(
            transport,
            bus,
            new SelectionAwareDiscoverer([new TestingDiscoveredTest("known", "Known", "known")]),
            PassThroughRunMapper.Instance,
            filter: null,
            cts.Token);

        Assert.Null(transport.LastRequest);
        var node = Assert.Single(bus.Nodes);
        Assert.Equal("devtools.testadapter.run", node.Uid.Value);
    }

    [Fact]
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
                new SelectionAwareDiscoverer([new TestingDiscoveredTest("known", "Known", "known")]),
                PassThroughRunMapper.Instance,
                filter: null,
                cts.Token),
            TestContext.Current.CancellationToken);

        Assert.True(runEntered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        cts.Cancel();
        blockRun.Set();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(transport.Cancelled);
    }

    private static Task ExecuteRunAsync(
        ITestRunnerTransport transport,
        IMessageBus bus,
        IHostTestDiscoverer discoverer,
        IHostTestRunMapper mapper,
        ITestExecutionFilter? filter,
        CancellationToken cancellationToken)
    {
        lock (DiscoveryProviderLock)
        {
            var previous = HostTestDiscovery.Current;
            HostTestDiscovery.Clear();
            HostTestDiscovery.Register(discoverer, mapper);
            try
            {
                var framework = new HostTestFramework(new StubServiceProvider(HostConfiguration()), transport);
                framework.ExecuteRequestAsync(CreateRunContext(bus, filter, cancellationToken)).GetAwaiter().GetResult();
            }
            finally
            {
                HostTestDiscovery.Clear();
                if (previous is not null)
                    HostTestDiscovery.Register(previous.Discoverer, previous.RunMapper);
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

    private static IConfiguration HostConfiguration() =>
        new StubConfiguration(new Dictionary<string, string?>
        {
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.HostName)] = "Revit",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.HostVersion)] = "2026",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.PerTestTimeoutSeconds)] = "60",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.LaunchTimeoutSeconds)] = "180",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.FrameworkId)] = "nunit",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.RunnerPath)] = @"C:\Runner.exe",
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

    private sealed class SelectionAwareDiscoverer(IReadOnlyList<TestingDiscoveredTest> tests) : IHostTestDiscoverer
    {
        public IReadOnlyList<TestingDiscoveredTest> Discover(string assemblyPath, TestingSelection selection)
        {
            if (selection.Kind == TestingSelectionKind.TestIds && selection.TestIds.Count == 0)
                return [];

            if (selection.Kind != TestingSelectionKind.TestIds)
                return tests;

            var ids = selection.TestIds.ToHashSet(StringComparer.Ordinal);
            return tests.Where(test => ids.Contains(test.TestId)).ToList();
        }
    }

    private sealed class UnreportedMapper : IHostTestRunMapper
    {
        public TestingSelection ToHostSelection(
            TestingSelection requested,
            IReadOnlyList<TestingDiscoveredTest> discovered) =>
            requested;

        public IReadOnlyList<TestingCaseResult> FoldResults(
            TestingSelection requested,
            IReadOnlyList<TestingDiscoveredTest> discovered,
            IReadOnlyList<TestingCaseResult> hostResults) =>
            hostResults;

        public IReadOnlyList<TestingCaseResult> ResultsForUnreported(
            TestingSelection requested,
            IReadOnlyList<TestingDiscoveredTest> discovered,
            IReadOnlyList<TestingCaseResult> hostResults)
        {
            var reported = hostResults.Select(result => result.TestId).ToHashSet(StringComparer.Ordinal);
            return discovered
                .Where(test => !reported.Contains(test.TestId))
                .Select(test => new TestingCaseResult(
                    test.TestId,
                    test.DisplayName,
                    TestingOutcomes.Error,
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
