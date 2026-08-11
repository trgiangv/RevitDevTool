using System.Text.Json;
using DevTools.NUnit.Core.Contracts;

namespace DevTools.NUnit.Core.Tests;

/// <summary>Golden JSON shape checks for the nunit/* bridge contract.</summary>
public sealed class NUnitProtocolGoldenTests
{
    private static readonly Guid SampleRunId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void Hello_Request_MatchesGoldenEnvelope()
    {
        var message = BridgeMessage.Request(
            "1",
            NUnitProtocol.Hello,
            JsonSerializer.SerializeToElement(
                new NUnitHelloRequest(NUnitProtocol.CurrentVersion),
                NUnitJsonContext.Default.NUnitHelloRequest));

        Assert.Equal(
            """{"type":"request","id":"1","method":"nunit/hello","params":{"protocol_version":1},"isError":false}""",
            SerializeBridgeMessage(message));
    }

    [Fact]
    public void Hello_Response_MatchesGoldenEnvelope()
    {
        var response = new NUnitHelloResponse(
            NUnitProtocol.CurrentVersion,
            "Revit",
            "2025",
            12345,
            false);

        var message = BridgeMessage.Response(
            "1",
            JsonSerializer.SerializeToElement(response, NUnitJsonContext.Default.NUnitHelloResponse));

        Assert.Equal(
            """
            {"type":"response","id":"1","result":{"protocol_version":1,"host":"Revit","host_version":"2025","process_id":12345,"is_busy":false},"isError":false}
            """.Trim(),
            SerializeBridgeMessage(message));
    }

    [Fact]
    public void Discover_Request_MatchesGoldenEnvelope()
    {
        var request = new NUnitDiscoverRequest(
            "C:\\tests\\SampleTests.dll",
            "cat==Integration");

        var message = BridgeMessage.Request(
            "2",
            NUnitProtocol.Discover,
            JsonSerializer.SerializeToElement(request, NUnitJsonContext.Default.NUnitDiscoverRequest));

        Assert.Equal(
            """
            {"type":"request","id":"2","method":"nunit/discover","params":{"assembly_path":"C:\\tests\\SampleTests.dll","filter":"cat==Integration"},"isError":false}
            """.Trim(),
            SerializeBridgeMessage(message));
    }

    [Fact]
    public void Discover_Response_MatchesGoldenEnvelope()
    {
        var response = new NUnitDiscoverResponse(
        [
            new NUnitDiscoveredTest(
                "SampleTests.Fixture.Pass",
                "Pass",
                "SampleTests.Fixture.Pass"),
        ]);

        var message = BridgeMessage.Response(
            "2",
            JsonSerializer.SerializeToElement(response, NUnitJsonContext.Default.NUnitDiscoverResponse));

        Assert.Equal(
            """
            {"type":"response","id":"2","result":{"cases":[{"id":"SampleTests.Fixture.Pass","name":"Pass","full_name":"SampleTests.Fixture.Pass"}]},"isError":false}
            """.Trim(),
            SerializeBridgeMessage(message));
    }

    [Fact]
    public void Run_Request_MatchesGoldenEnvelope()
    {
        var request = new NUnitRunRequest(
            SampleRunId,
            "C:\\tests\\SampleTests.dll",
            "name==Pass",
            true);

        var message = BridgeMessage.Request(
            "3",
            NUnitProtocol.Run,
            JsonSerializer.SerializeToElement(request, NUnitJsonContext.Default.NUnitRunRequest));

        Assert.Equal(
            """
            {"type":"request","id":"3","method":"nunit/run","params":{"run_id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","assembly_path":"C:\\tests\\SampleTests.dll","filter":"name==Pass","wait_for_debugger":true},"isError":false}
            """.Trim(),
            SerializeBridgeMessage(message));
    }

    [Fact]
    public void Run_Response_MatchesGoldenEnvelope()
    {
        var response = new NUnitRunResponse(
            SampleRunId,
            new NUnitRunSummary(1, 1, 0, 0, 0, 0),
            [
                new NUnitCaseResult(
                    "SampleTests.Fixture.Pass",
                    "Pass",
                    "Passed",
                    12.5,
                    null,
                    null,
                    "sample stdout"),
                new NUnitCaseResult(
                    "SampleTests.Fixture.Fail",
                    "Fail",
                    "Failed",
                    8.2,
                    "Expected: True\n  But was: False",
                    "at SampleTests.Fixture.Fail() in C:\\tests\\SampleTests.cs:line 42",
                    null),
            ]);

        var message = BridgeMessage.Response(
            "3",
            JsonSerializer.SerializeToElement(response, NUnitJsonContext.Default.NUnitRunResponse));

        Assert.Equal(
            """
            {"type":"response","id":"3","result":{"run_id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","summary":{"passed":1,"failed":1,"skipped":0,"inconclusive":0,"errors":0,"cancelled":0},"cases":[{"id":"SampleTests.Fixture.Pass","name":"Pass","outcome":"Passed","duration_ms":12.5,"output":"sample stdout"},{"id":"SampleTests.Fixture.Fail","name":"Fail","outcome":"Failed","duration_ms":8.2,"message":"Expected: True\n  But was: False","stack_trace":"at SampleTests.Fixture.Fail() in C:\\tests\\SampleTests.cs:line 42"}]},"isError":false}
            """.Trim(),
            SerializeBridgeMessage(message));
    }

    [Fact]
    public void Progress_Notification_MatchesGoldenEnvelope()
    {
        var progress = new NUnitProgressEvent(
            SampleRunId,
            new NUnitCaseResult(
                "SampleTests.Fixture.Pass",
                "Pass",
                "Passed",
                12.5,
                null,
                null,
                null));

        var message = BridgeMessage.Notification(
            NUnitProtocol.Progress,
            JsonSerializer.SerializeToElement(progress, NUnitJsonContext.Default.NUnitProgressEvent));

        Assert.Equal(
            """
            {"type":"notification","method":"nunit/progress","params":{"run_id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","case":{"id":"SampleTests.Fixture.Pass","name":"Pass","outcome":"Passed","duration_ms":12.5}},"isError":false}
            """.Trim(),
            SerializeBridgeMessage(message));
    }

    [Fact]
    public void DebugReady_Notification_MatchesGoldenEnvelope()
    {
        var debugReady = new NUnitDebugReadyEvent(SampleRunId, 4242);

        var message = BridgeMessage.Notification(
            NUnitProtocol.DebugReady,
            JsonSerializer.SerializeToElement(debugReady, NUnitJsonContext.Default.NUnitDebugReadyEvent));

        Assert.Equal(
            """
            {"type":"notification","method":"nunit/debug-ready","params":{"run_id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","process_id":4242},"isError":false}
            """.Trim(),
            SerializeBridgeMessage(message));
    }

    [Fact]
    public void DiscoverRequest_RoundTripsFilterAndAssemblyPath()
    {
        var original = new NUnitDiscoverRequest("C:\\tests\\SampleTests.dll", "cat==Integration");
        var json = JsonSerializer.Serialize(original, NUnitJsonContext.Default.NUnitDiscoverRequest);
        var roundTrip = JsonSerializer.Deserialize(json, NUnitJsonContext.Default.NUnitDiscoverRequest);

        Assert.NotNull(roundTrip);
        Assert.Equal(original.AssemblyPath, roundTrip.AssemblyPath);
        Assert.Equal(original.Filter, roundTrip.Filter);
    }

    [Fact]
    public void CaseResult_RoundTripsFailureDetails()
    {
        var original = new NUnitCaseResult(
            "SampleTests.Fixture.Fail",
            "Fail",
            "Failed",
            8.2,
            "Expected: True\n  But was: False",
            "at SampleTests.Fixture.Fail() in C:\\tests\\SampleTests.cs:line 42",
            "captured output");

        var json = JsonSerializer.Serialize(original, NUnitJsonContext.Default.NUnitCaseResult);
        var roundTrip = JsonSerializer.Deserialize(json, NUnitJsonContext.Default.NUnitCaseResult);

        Assert.NotNull(roundTrip);
        Assert.Equal(original.Id, roundTrip.Id);
        Assert.Equal(original.Name, roundTrip.Name);
        Assert.Equal(original.Outcome, roundTrip.Outcome);
        Assert.Equal(original.DurationMilliseconds, roundTrip.DurationMilliseconds);
        Assert.Equal(original.Message, roundTrip.Message);
        Assert.Equal(original.StackTrace, roundTrip.StackTrace);
        Assert.Equal(original.Output, roundTrip.Output);
    }

    private static string SerializeBridgeMessage(BridgeMessage message) =>
        JsonSerializer.Serialize(message, IpcJsonContext.Default.BridgeMessage);
}
