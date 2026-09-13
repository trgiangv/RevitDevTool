using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.NUnit.Runtime.Tests;

[TestClass]
[DoNotParallelize]
public sealed class NUnitRuntimeSessionMtpTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Run_returns_neutral_results_and_events()
    {
        using var session = FixtureTestHarness.CreateSession();
        var sink = new RecordingSink();
        var response = session.Run(CreateRequest(null), sink, TestContext.CancellationToken);

        Assert.AreEqual(TestFrameworkId.NUnit, response.FrameworkId);
        Assert.AreEqual(FixtureTestHarness.GenerationId, response.GenerationId);
        Assert.AreEqual(38, response.Results.Count);
        Assert.IsTrue(response.Results.Any(result  => result.DisplayName == "PlainTest_Passes" && result.Outcome == TestOutcomes.Passed));
        Assert.IsTrue(sink.Events.Any(testingEvent  => testingEvent.Kind == TestEventKinds.Case && testingEvent.Case is not null));
    }

    [TestMethod]
    public void Run_sets_nunit_work_directory_to_the_assembly_directory()
    {
        using var session = FixtureTestHarness.CreateSession();
        const string fullName = "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes";
        var response = session.Run(
            CreateRequest("<filter><test>" + fullName + "</test></filter>"),
            new RecordingSink(),
            TestContext.CancellationToken);

        var result = response.Results.Single();
        Assert.AreEqual(fullName, result.FullName);
        Assert.AreEqual(fullName, result.TestId);
        Assert.AreEqual(TestOutcomes.Passed, result.Outcome);
    }

    [TestMethod]
    public void Run_collapsed_fixture_source_full_name_selects_expanded_leaves()
    {
        using var session = FixtureTestHarness.CreateSession();
        const string stubId =
            "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture.FixtureSource_ValueIsPreserved";
        var xml = NUnitCollapsedSelection.ToFilterXml([stubId]);
        var response = session.Run(
            CreateRequest(xml),
            new RecordingSink(),
            TestContext.CancellationToken);

        Assert.AreEqual(2, response.Results.Count);
        foreach (var result in response.Results)
        {
            Assert.AreEqual("Passed", result.Outcome);
            Assert.Contains("ParameterizedFixture(", result.TestId, StringComparison.Ordinal);
            Assert.Contains("FixtureSource_ValueIsPreserved", result.TestId, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void Run_collapsed_setname_full_name_selects_renamed_leaves()
    {
        using var session = FixtureTestHarness.CreateSession();
        const string stubId =
            "DevTools.NUnit.Runtime.Fixtures.SetNameCaseSourceFixture.Original_method";
        var xml = NUnitCollapsedSelection.ToFilterXml([stubId]);
        var response = session.Run(
            CreateRequest(xml),
            new RecordingSink(),
            TestContext.CancellationToken);

        Assert.AreEqual(2, response.Results.Count);
        Assert.AreSequenceEqual(
            ["Renamed_one", "Renamed_two"],
            response.Results.Select(result => result.DisplayName).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.IsTrue(response.Results.All(result => result.ParentTestId == stubId));
    }

    [TestMethod]
    public void Run_collapsed_testname_full_name_selects_named_leaves()
    {
        using var session = FixtureTestHarness.CreateSession();
        const string stubId =
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named";
        var xml = NUnitCollapsedSelection.ToFilterXml([stubId]);
        var response = session.Run(
            CreateRequest(xml),
            new RecordingSink(),
            TestContext.CancellationToken);

        Assert.AreEqual(2, response.Results.Count);
        Assert.AreSequenceEqual(
            ["Named_one", "Named_two"],
            response.Results.Select(result => result.DisplayName).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.IsTrue(response.Results.All(result => result.ParentTestId == stubId));
    }

    [TestMethod]
    public void Run_parameterized_case_with_args_stays_one_leaf()
    {
        using var session = FixtureTestHarness.CreateSession();
        const string fullName =
            "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCase_Addition(1,1,2)";
        var response = session.Run(
            CreateRequest("<filter><test>" + fullName + "</test></filter>"),
            new RecordingSink(),
            TestContext.CancellationToken);

        var result = response.Results.Single();
        Assert.AreEqual(fullName, result.TestId);
        Assert.AreEqual(fullName, result.FullName);
        Assert.AreEqual("TestCase_Addition(1,1,2)", result.DisplayName);
    }

    [TestMethod]
    public void Run_reports_each_leaf_when_display_names_collide()
    {
        using var session = DedicatedTestFixturesHarness.CreateSession();
        var request = new TestRunRequest(
            1,
            Guid.NewGuid(),
            TestFrameworkId.NUnit,
            new TestAssemblyReference(DedicatedTestFixturesHarness.AssemblyPath),
            TestSelection.FromFrameworkFilter(
                "filter-xml",
                DedicatedTestFixturesHarness.DuplicateNameFilter));
        var response = session.Run(request, new RecordingSink(), TestContext.CancellationToken);

        Assert.AreEqual(2, response.Results.Count);
        foreach (var result in response.Results)
        {
            Assert.AreEqual("SharedDisplayName", result.DisplayName);
            Assert.AreEqual(result.FullName, result.TestId);
        }
    }

    [TestMethod]
    public void Run_applies_provider_filter_without_discovery_contract()
    {
        using var session = FixtureTestHarness.CreateSession();
        const string fullName = "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes";
        var response = session.Run(
            CreateRequest("<filter><test>" + fullName + "</test></filter>"),
            new RecordingSink(),
            TestContext.CancellationToken);

        var result = response.Results.Single();
        Assert.AreEqual(fullName, result.FullName);
        Assert.AreEqual(TestOutcomes.Passed, result.Outcome);
    }

    [TestMethod]
    public async Task Cancel_stops_a_blocking_run_through_the_neutral_contract()
    {
        DedicatedTestFixturesHarness.ResetBlockingState();
        using var session = DedicatedTestFixturesHarness.CreateSession();
        var runId = Guid.NewGuid();
        var request = new TestRunRequest(
            1,
            runId,
            TestFrameworkId.NUnit,
            new TestAssemblyReference(DedicatedTestFixturesHarness.AssemblyPath),
            TestSelection.FromFrameworkFilter(
                "filter-xml",
                DedicatedTestFixturesHarness.BlockingFilter));
        var runTask = Task.Run(() => session.Run(request, new RecordingSink(), CancellationToken.None));

        Assert.IsTrue(SpinWait.SpinUntil(
            () => Volatile.Read(ref Fixtures.BlockingRunState.Entered) == 1,
            TimeSpan.FromSeconds(15)));
        session.Cancel(runId);
        Volatile.Write(ref Fixtures.BlockingRunState.Release, 1);

        var response = await runTask.WaitAsync(TimeSpan.FromSeconds(15), TestContext.CancellationToken);
        Assert.AreEqual(TestOutcomes.Cancelled, response.Results.Single().Outcome);
        Assert.AreEqual(TestCancellationState.Completed, response.CancellationState);
    }

    private static TestRunRequest CreateRequest(string? filter) => new(
        1,
        Guid.NewGuid(),
        TestFrameworkId.NUnit,
        new TestAssemblyReference(FixtureTestHarness.FixtureAssemblyPath),
        SelectionFromFilter(filter));

    private static TestSelection SelectionFromFilter(string? filter) =>
        string.IsNullOrWhiteSpace(filter)
            ? TestSelection.All
            : TestSelection.FromFrameworkFilter("filter-xml", filter);

    private sealed class RecordingSink : ITestingRuntimeEventSink
    {
        internal List<TestEvent> Events { get; } = [];
        public void Publish(TestEvent testingEvent) => Events.Add(testingEvent);
    }
}
