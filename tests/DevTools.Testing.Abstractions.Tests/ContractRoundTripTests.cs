using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

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
        Assert.Equal(testId, new TestingSelection([testId]).TestIds[0]);
    }

    [Fact]
    public void Every_cancellation_state_round_trips_on_events_and_responses()
    {
        foreach (TestingCancellationState state in Enum.GetValues<TestingCancellationState>())
        {
            var runId = Guid.NewGuid();
            var testingEvent = new TestingEvent(
                runId,
                TestingEventKinds.Cancellation,
                Case: null,
                Message: state.ToString(),
                Attachment: null,
                CancellationState: state);
            var response = new TestingRunResponse(
                runId,
                "provider.example",
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
    public void Case_result_round_trips_hierarchy_provider_payload_and_complete_attachment()
    {
        var attachment = new TestingAttachment(
            Description: "trace",
            ContentType: "text/plain",
            Path: @"C:\\temp\\trace.txt",
            Base64: "dHJhY2U=");
        var payload = new TestingProviderPayload("provider.example/result", 3, "opaque-data");
        var result = new TestingCaseResult(
            "case-1",
            "Display case",
            "Skipped",
            0,
            Message: null,
            StackTrace: null,
            Output: null,
            Source: new TestingSourceLocation("Fixture.cs", 12),
            Traits: [new TestingTrait("Category", "Acceptance")],
            Attachments: [attachment],
            ParentTestId: "suite-1",
            FullName: "Provider.Fixture.DisplayCase",
            SkipReason: "requires capability",
            ProviderPayload: payload);

        Assert.Equal("suite-1", result.ParentTestId);
        Assert.Equal("Provider.Fixture.DisplayCase", result.FullName);
        Assert.Equal("requires capability", result.SkipReason);
        Assert.Same(payload, result.ProviderPayload);
        var roundTripAttachment = Assert.Single(result.Attachments);
        Assert.Equal("trace", roundTripAttachment.Description);
        Assert.Equal("text/plain", roundTripAttachment.ContentType);
        Assert.Equal(@"C:\\temp\\trace.txt", roundTripAttachment.Path);
        Assert.Equal("dHJhY2U=", roundTripAttachment.Base64);
    }

    [Fact]
    public void Runtime_event_preserves_case_attachment_and_cancellation_state()
    {
        var runId = Guid.NewGuid();
        var attachment = new TestingAttachment("trace.txt", "trace", "text/plain", "dHJhY2U=");
        var testingEvent = new TestingRuntimeEvent(
            runId,
            TestingEventKinds.Attachment,
            CreateCase("case-2"),
            "saved trace",
            attachment,
            TestingCancellationState.Acknowledged);

        Assert.Equal(runId, testingEvent.RunId);
        Assert.Equal("case-2", testingEvent.Case!.TestId);
        Assert.Equal("trace.txt", testingEvent.Attachment!.Path);
        Assert.Equal(TestingCancellationState.Acknowledged, testingEvent.CancellationState);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Run_request_rejects_an_empty_provider_owned_framework_id(string frameworkId)
    {
        var exception = Assert.Throws<ArgumentException>(() => new TestingRunRequest(
            ProtocolVersion: 1,
            RunId: Guid.NewGuid(),
            FrameworkId: frameworkId,
            Assembly: new TestingAssemblyReference("tests.dll", "net10.0", null),
            Selection: new TestingSelection([], null),
            FrameworkOptions: new Dictionary<string, string>()));

        Assert.Equal("FrameworkId", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Run_request_with_expression_rejects_an_empty_provider_owned_framework_id(string frameworkId)
    {
        var request = new TestingRunRequest(
            ProtocolVersion: 1,
            RunId: Guid.NewGuid(),
            FrameworkId: "provider.example",
            Assembly: new TestingAssemblyReference("tests.dll", "net10.0", null),
            Selection: new TestingSelection([], null),
            FrameworkOptions: new Dictionary<string, string>());

        var exception = Assert.Throws<ArgumentException>(() =>
            request with { FrameworkId = frameworkId });

        Assert.Equal("FrameworkId", exception.ParamName);
    }

    static TestingCaseResult CreateCase(string testId)
        => new(
            testId,
            DisplayName: "display",
            Outcome: "Passed",
            DurationMilliseconds: 1,
            Message: null,
            StackTrace: null,
            Output: null,
            Source: new TestingSourceLocation("file.cs", 10),
            Traits: [],
            Attachments: []);

    static string NormalizeAway(string original, string stored)
        => original == stored ? string.Empty : stored;
}
