using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.NUnit.Runtime.Tests;

[CollectionDefinition(nameof(BlockingFixtureCollection), DisableParallelization = true)]
public sealed class BlockingFixtureCollection;

[Collection(nameof(BlockingFixtureCollection))]
public sealed class NUnitRuntimeSessionMtpTests
{
    [Fact]
    public void Run_returns_neutral_results_and_events()
    {
        using var session = FixtureTestHarness.CreateSession();
        var sink = new RecordingSink();
        var response = session.Run(CreateRequest(null), sink, TestContext.Current.CancellationToken);

        Assert.Equal("nunit", response.FrameworkId);
        Assert.Equal(FixtureTestHarness.GenerationId, response.GenerationId);
        Assert.Equal(38, response.Results.Count);
        Assert.Contains(response.Results, result =>
            result.DisplayName == "PlainTest_Passes" && result.Outcome == TestingOutcomes.Passed);
        Assert.Contains(sink.Events, testingEvent =>
            testingEvent.Kind == TestingEventKinds.Case && testingEvent.Case is not null);
    }

    [Fact]
    public void Run_sets_nunit_work_directory_to_the_assembly_directory()
    {
        using var session = FixtureTestHarness.CreateSession();
        const string fullName = "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes";
        var response = session.Run(
            CreateRequest("<filter><test>" + fullName + "</test></filter>"),
            new RecordingSink(),
            TestContext.Current.CancellationToken);

        var result = Assert.Single(response.Results);
        Assert.Equal(fullName, result.FullName);
        Assert.Equal(fullName, result.TestId);
        Assert.Equal(TestingOutcomes.Passed, result.Outcome);
    }

    [Fact]
    public void Run_collapsed_fixture_source_full_name_selects_expanded_leaves()
    {
        using var session = FixtureTestHarness.CreateSession();
        const string stubId =
            "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture.FixtureSource_ValueIsPreserved";
        var xml = NUnitCollapsedSelection.ToFilterXml([stubId]);
        var response = session.Run(
            CreateRequest(xml),
            new RecordingSink(),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, response.Results.Count);
        Assert.All(response.Results, result =>
        {
            Assert.Equal("Passed", result.Outcome);
            Assert.Contains("ParameterizedFixture(", result.TestId, StringComparison.Ordinal);
            Assert.Contains("FixtureSource_ValueIsPreserved", result.TestId, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Run_collapsed_setname_full_name_selects_renamed_leaves()
    {
        using var session = FixtureTestHarness.CreateSession();
        const string stubId =
            "DevTools.NUnit.Runtime.Fixtures.SetNameCaseSourceFixture.Original_method";
        var xml = NUnitCollapsedSelection.ToFilterXml([stubId]);
        var response = session.Run(
            CreateRequest(xml),
            new RecordingSink(),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, response.Results.Count);
        Assert.Equal(
            ["Renamed_one", "Renamed_two"],
            response.Results.Select(result => result.DisplayName).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.All(response.Results, result => Assert.Equal(stubId, result.ParentTestId));
    }

    [Fact]
    public void Run_collapsed_testname_full_name_selects_named_leaves()
    {
        using var session = FixtureTestHarness.CreateSession();
        const string stubId =
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named";
        var xml = NUnitCollapsedSelection.ToFilterXml([stubId]);
        var response = session.Run(
            CreateRequest(xml),
            new RecordingSink(),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, response.Results.Count);
        Assert.Equal(
            ["Named_one", "Named_two"],
            response.Results.Select(result => result.DisplayName).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.All(response.Results, result => Assert.Equal(stubId, result.ParentTestId));
    }

    [Fact]
    public void Run_parameterized_case_with_args_stays_one_leaf()
    {
        using var session = FixtureTestHarness.CreateSession();
        const string fullName =
            "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCase_Addition(1,1,2)";
        var response = session.Run(
            CreateRequest("<filter><test>" + fullName + "</test></filter>"),
            new RecordingSink(),
            TestContext.Current.CancellationToken);

        var result = Assert.Single(response.Results);
        Assert.Equal(fullName, result.TestId);
        Assert.Equal(fullName, result.FullName);
        Assert.Equal("TestCase_Addition(1,1,2)", result.DisplayName);
    }

    [Fact]
    public void Run_reports_each_leaf_when_display_names_collide()
    {
        using var session = DedicatedTestFixturesHarness.CreateSession();
        var request = new TestingRunRequest(
            1,
            Guid.NewGuid(),
            "nunit",
            new TestingAssemblyReference(DedicatedTestFixturesHarness.AssemblyPath, "net10.0-windows", null),
            new TestingSelection([], DedicatedTestFixturesHarness.DuplicateNameFilter),
            new Dictionary<string, string>());
        var response = session.Run(request, new RecordingSink(), TestContext.Current.CancellationToken);

        Assert.Equal(2, response.Results.Count);
        Assert.All(response.Results, result =>
        {
            Assert.Equal("SharedDisplayName", result.DisplayName);
            Assert.Equal(result.FullName, result.TestId);
        });
    }

    [Fact]
    public void Run_applies_provider_filter_without_discovery_contract()
    {
        using var session = FixtureTestHarness.CreateSession();
        const string fullName = "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes";
        var response = session.Run(
            CreateRequest("<filter><test>" + fullName + "</test></filter>"),
            new RecordingSink(),
            TestContext.Current.CancellationToken);

        var result = Assert.Single(response.Results);
        Assert.Equal(fullName, result.FullName);
        Assert.Equal(TestingOutcomes.Passed, result.Outcome);
    }

    [Fact]
    public async Task Cancel_stops_a_blocking_run_through_the_neutral_contract()
    {
        DedicatedTestFixturesHarness.ResetBlockingState();
        using var session = DedicatedTestFixturesHarness.CreateSession();
        var runId = Guid.NewGuid();
        var request = new TestingRunRequest(
            1,
            runId,
            "nunit",
            new TestingAssemblyReference(DedicatedTestFixturesHarness.AssemblyPath, "net10.0-windows", null),
            new TestingSelection([], DedicatedTestFixturesHarness.BlockingFilter),
            new Dictionary<string, string>());
        var runTask = Task.Run(() => session.Run(request, new RecordingSink(), CancellationToken.None));

        Assert.True(SpinWait.SpinUntil(
            () => Volatile.Read(ref Fixtures.BlockingRunState.Entered) == 1,
            TimeSpan.FromSeconds(15)));
        session.Cancel(runId);
        Volatile.Write(ref Fixtures.BlockingRunState.Release, 1);

        var response = await runTask.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        Assert.Equal(TestingOutcomes.Cancelled, Assert.Single(response.Results).Outcome);
        Assert.Equal(TestingCancellationState.Completed, response.CancellationState);
    }

    private static TestingRunRequest CreateRequest(string? filter) => new(
        1,
        Guid.NewGuid(),
        "nunit",
        new TestingAssemblyReference(FixtureTestHarness.FixtureAssemblyPath, "net10.0-windows", null),
        new TestingSelection([], filter),
        new Dictionary<string, string>());

    private sealed class RecordingSink : ITestingRuntimeEventSink
    {
        internal List<TestingRuntimeEvent> Events { get; } = [];
        public void Publish(TestingRuntimeEvent testingEvent) => Events.Add(testingEvent);
    }
}
