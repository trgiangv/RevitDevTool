using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

public sealed class ContractRoundTripTests
{
    public static TheoryData<string> OpaqueIds =>
        new()
        {
            "  leading and trailing whitespace  ",
            "NUnit.Name(\"a,b\")::Method(1)",
            "provider.v1://method/Future.Framework.Fixtures.TheoryFixture.Inline(input: 42)/0",
            @"C:\path with spaces\Test.cs:12",
            "id/with/slashes+plus&amp;punct!",
        };

    [Theory]
    [MemberData(nameof(OpaqueIds))]
    public void Test_ids_round_trip_without_fqn_normalization(string testId)
    {
        var result = CreateCase(testId);
        Assert.Equal(testId, result.TestId);
        Assert.DoesNotContain("::", NormalizeAway(testId, result.TestId));
        Assert.Equal(testId, TestSelection.FromTestIds([testId]).TestIds[0]);
    }

    [Fact]
    public void Empty_test_ids_is_constrained_run_nothing_not_all()
    {
        var empty = TestSelection.FromTestIds([]);
        Assert.Equal(TestSelectionKind.TestIds, empty.Kind);
        Assert.True(empty.IsConstrained);
        Assert.Empty(empty.TestIds);
        Assert.NotEqual(TestSelection.All, empty);
        Assert.False(TestSelection.All.IsConstrained);
    }

    [Fact]
    public void Unknown_kind_throws_for_constructor_parameter()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TestSelection((TestSelectionKind)99));
        Assert.Equal("kind", ex.ParamName);
    }

    [Fact]
    public void All_rejects_ids_names_and_framework_filter()
    {
        Assert.Throws<ArgumentException>(() =>
            new TestSelection(TestSelectionKind.All, testIds: ["id"]));
        Assert.Throws<ArgumentException>(() =>
            new TestSelection(TestSelectionKind.All, names: ["name"]));
        Assert.Throws<ArgumentException>(() =>
            new TestSelection(TestSelectionKind.All, filterFormat: "nunit", filterData: "<filter/>"));
    }

    [Fact]
    public void Every_cancellation_state_round_trips_on_events_and_responses()
    {
        foreach (TestCancellationState state in Enum.GetValues<TestCancellationState>())
        {
            var runId = Guid.NewGuid();
            var testingEvent = new TestEvent(
                runId,
                TestEventKinds.Cancellation,
                Case: null,
                Message: state.ToString(),
                Attachment: null,
                CancellationState: state);
            var response = new TestRunResponse(
                runId,
                TestFrameworkId.NUnit,
                GenerationId: "gen-1",
                Results: [],
                CancellationState: state,
                DiagnosticCode: null,
                DiagnosticMessage: null);

            Assert.Equal(state, testingEvent.CancellationState);
            Assert.Equal(state, response.CancellationState);
            Assert.Equal(runId, testingEvent.RunId);
            Assert.Equal(runId, response.RunId);
        }
    }

    [Fact]
    public void Case_result_round_trips_hierarchy_and_skip_reason()
    {
        var attachment = new TestAttachment(
            Path: @"C:\\temp\\trace.txt",
            Description: "trace",
            ContentType: "text/plain");
        var result = new TestCaseResult(
            "case-1",
            "Display case",
            "Skipped",
            0,
            Message: null,
            StackTrace: null,
            Output: null,
            Source: new TestSourceLocation("Fixture.cs", 12),
            Traits: [new TestTrait("Category", "Acceptance")],
            Attachments: [attachment],
            ParentTestId: "suite-1",
            FullName: "Provider.Fixture.DisplayCase",
            SkipReason: "requires capability");

        Assert.Equal("suite-1", result.ParentTestId);
        Assert.Equal("Provider.Fixture.DisplayCase", result.FullName);
        Assert.Equal("requires capability", result.SkipReason);
        var roundTripAttachment = Assert.Single(result.Attachments);
        Assert.Equal("trace", roundTripAttachment.Description);
        Assert.Equal("text/plain", roundTripAttachment.ContentType);
        Assert.Equal(@"C:\\temp\\trace.txt", roundTripAttachment.Path);
    }

    [Fact]
    public void Event_preserves_case_attachment_and_cancellation_state()
    {
        var runId = Guid.NewGuid();
        var attachment = new TestAttachment("trace.txt", "trace", "text/plain");
        var testingEvent = new TestEvent(
            runId,
            TestEventKinds.Attachment,
            CreateCase("case-2"),
            "saved trace",
            attachment,
            TestCancellationState.Acknowledged);

        Assert.Equal(runId, testingEvent.RunId);
        Assert.Equal("case-2", testingEvent.Case!.TestId);
        Assert.Equal("trace.txt", testingEvent.Attachment!.Path);
        Assert.Equal(TestCancellationState.Acknowledged, testingEvent.CancellationState);
    }

    [Fact]
    public void Run_request_rejects_an_undefined_framework_id()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TestRunRequest(
            ProtocolVersion: 1,
            RunId: Guid.NewGuid(),
            FrameworkId: (TestFrameworkId)42,
            Assembly: new TestAssemblyReference("tests.dll"),
            Selection: TestSelection.All));

        Assert.Equal("FrameworkId", exception.ParamName);
    }

    [Fact]
    public void Run_request_with_expression_rejects_an_undefined_framework_id()
    {
        var request = new TestRunRequest(
            ProtocolVersion: 1,
            RunId: Guid.NewGuid(),
            FrameworkId: TestFrameworkId.NUnit,
            Assembly: new TestAssemblyReference("tests.dll"),
            Selection: TestSelection.All);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            request with { FrameworkId = (TestFrameworkId)42 });

        Assert.Equal("FrameworkId", exception.ParamName);
    }

    static TestCaseResult CreateCase(string testId)
        => new(
            testId,
            DisplayName: "display",
            Outcome: "Passed",
            DurationMilliseconds: 1,
            Message: null,
            StackTrace: null,
            Output: null,
            Source: new TestSourceLocation("file.cs", 10),
            Traits: [],
            Attachments: []);

    static string NormalizeAway(string original, string stored)
        => original == stored ? string.Empty : stored;
}
