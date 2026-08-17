using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

public sealed class ContractRoundTripTests
{
    public static TheoryData<string> OpaqueIds =>
        new()
        {
            "  leading and trailing whitespace  ",
            "NUnit.Name(\"a,b\")::Method(1)",
            "xunit.v3://method/DevTools.Xunit.Runtime.Fixtures.TheoryFixture.Inline(input: 42)/0",
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
                TestingFrameworkIds.NUnit,
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
    public void Framework_ids_are_stable_literals()
    {
        Assert.Equal("nunit", TestingFrameworkIds.NUnit);
        Assert.Equal("xunit", TestingFrameworkIds.Xunit);
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
