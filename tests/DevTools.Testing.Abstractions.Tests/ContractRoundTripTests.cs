using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

[TestClass]
public sealed class ContractRoundTripTests
{
    public static IEnumerable<object[]> OpaqueIds()
    {
        yield return new object[] { "  leading and trailing whitespace  " };
        yield return new object[] { "NUnit.Name(\"a,b\")::Method(1)" };
        yield return new object[] { "provider.v1://method/Future.Framework.Fixtures.TheoryFixture.Inline(input: 42)/0" };
        yield return new object[] { @"C:\path with spaces\Test.cs:12" };
        yield return new object[] { "id/with/slashes+plus&amp;punct!" };
    }

    [TestMethod]
    [DynamicData(nameof(OpaqueIds))]
    public void Test_ids_round_trip_without_fqn_normalization(string testId)
    {
        var result = CreateCase(testId);
        Assert.AreEqual(testId, result.TestId);
        Assert.DoesNotContain("::", NormalizeAway(testId, result.TestId));
        Assert.AreEqual(testId, TestSelection.FromTestIds([testId]).TestIds[0]);
    }

    [TestMethod]
    public void Empty_test_ids_is_constrained_run_nothing_not_all()
    {
        var empty = TestSelection.FromTestIds([]);
        Assert.AreEqual(TestSelectionKind.TestIds, empty.Kind);
        Assert.IsTrue(empty.IsConstrained);
        Assert.IsEmpty(empty.TestIds);
        Assert.AreNotEqual(TestSelection.All, empty);
        Assert.IsFalse(TestSelection.All.IsConstrained);
    }

    [TestMethod]
    public void Unknown_kind_throws_for_constructor_parameter()
    {
        var ex = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new TestSelection((TestSelectionKind)99));
        Assert.AreEqual("kind", ex.ParamName);
    }

    [TestMethod]
    public void All_rejects_ids_names_and_framework_filter()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new TestSelection(TestSelectionKind.All, testIds: ["id"]));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new TestSelection(TestSelectionKind.All, names: ["name"]));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new TestSelection(TestSelectionKind.All, filterFormat: "nunit", filterData: "<filter/>"));
    }

    [TestMethod]
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

            Assert.AreEqual(state, testingEvent.CancellationState);
            Assert.AreEqual(state, response.CancellationState);
            Assert.AreEqual(runId, testingEvent.RunId);
            Assert.AreEqual(runId, response.RunId);
        }
    }

    [TestMethod]
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

        Assert.AreEqual("suite-1", result.ParentTestId);
        Assert.AreEqual("Provider.Fixture.DisplayCase", result.FullName);
        Assert.AreEqual("requires capability", result.SkipReason);
        var roundTripAttachment = Assert.ContainsSingle(result.Attachments);
        Assert.AreEqual("trace", roundTripAttachment.Description);
        Assert.AreEqual("text/plain", roundTripAttachment.ContentType);
        Assert.AreEqual(@"C:\\temp\\trace.txt", roundTripAttachment.Path);
    }

    [TestMethod]
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

        Assert.AreEqual(runId, testingEvent.RunId);
        Assert.AreEqual("case-2", testingEvent.Case!.TestId);
        Assert.AreEqual("trace.txt", testingEvent.Attachment!.Path);
        Assert.AreEqual(TestCancellationState.Acknowledged, testingEvent.CancellationState);
    }

    [TestMethod]
    public void Run_request_rejects_an_undefined_framework_id()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TestRunRequest(
            ProtocolVersion: 1,
            RunId: Guid.NewGuid(),
            FrameworkId: (TestFrameworkId)42,
            Assembly: new TestAssemblyReference("tests.dll"),
            Selection: TestSelection.All));

        Assert.AreEqual("FrameworkId", exception.ParamName);
    }

    [TestMethod]
    public void Run_request_with_expression_rejects_an_undefined_framework_id()
    {
        var request = new TestRunRequest(
            ProtocolVersion: 1,
            RunId: Guid.NewGuid(),
            FrameworkId: TestFrameworkId.NUnit,
            Assembly: new TestAssemblyReference("tests.dll"),
            Selection: TestSelection.All);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            request with { FrameworkId = (TestFrameworkId)42 });

        Assert.AreEqual("FrameworkId", exception.ParamName);
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
