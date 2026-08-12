using System.Text.Json;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Transport;

namespace DevTools.NUnit.Core.Tests;

/// <summary>Golden JSON shape checks for the nunit/* bridge contract.</summary>
public sealed class NUnitProtocolGoldenTests
{
    private static readonly Guid SampleRunId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private const string SampleGenerationId = "gen-7f3c2a1b9e4d8c0f6a2b5d9e1c4f7a0b";

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
            """{"type":"request","id":"1","method":"nunit/hello","params":{"protocol_version":2},"isError":false}""",
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
            {"type":"response","id":"1","result":{"protocol_version":2,"host":"Revit","host_version":"2025","process_id":12345,"is_busy":false},"isError":false}
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
                "SampleTests.Fixture.Pass",
                ParentTestId: "SampleTests.Fixture",
                Traits:
                [
                    new NUnitTrait("Category", "Integration"),
                    new NUnitTrait("Author", "DevTools"),
                ],
                Source: new NUnitSourceLocation("C:\\tests\\SampleTests.cs", 18),
                SkipReason: null),
        ],
            GenerationId: SampleGenerationId,
            RuntimeDiagnostic: new NUnitRuntimeDiagnostic(
                "generation.loaded",
                "Generation loaded from shadow directory."));

        var message = BridgeMessage.Response(
            "2",
            JsonSerializer.SerializeToElement(response, NUnitJsonContext.Default.NUnitDiscoverResponse));

        Assert.Equal(
            """
            {"type":"response","id":"2","result":{"cases":[{"id":"SampleTests.Fixture.Pass","name":"Pass","full_name":"SampleTests.Fixture.Pass","parent_test_id":"SampleTests.Fixture","traits":[{"name":"Category","value":"Integration"},{"name":"Author","value":"DevTools"}],"source":{"file":"C:\\tests\\SampleTests.cs","line":18}}],"generation_id":"gen-7f3c2a1b9e4d8c0f6a2b5d9e1c4f7a0b","runtime_diagnostic":{"code":"generation.loaded","message":"Generation loaded from shadow directory."}},"isError":false}
            """.Trim(),
            SerializeBridgeMessage(message));
    }

    [Fact]
    public void Run_Request_MatchesGoldenEnvelope()
    {
        var request = new NUnitRunRequest(
            SampleRunId,
            "C:\\tests\\SampleTests.dll",
            "name==Pass");

        var message = BridgeMessage.Request(
            "3",
            NUnitProtocol.Run,
            JsonSerializer.SerializeToElement(request, NUnitJsonContext.Default.NUnitRunRequest));

        Assert.Equal(
            """
            {"type":"request","id":"3","method":"nunit/run","params":{"run_id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","assembly_path":"C:\\tests\\SampleTests.dll","filter":"name==Pass"},"isError":false}
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
                    "sample stdout",
                    ParentTestId: "SampleTests.Fixture",
                    Traits: [new NUnitTrait("Category", "Integration")],
                    Source: new NUnitSourceLocation("C:\\tests\\SampleTests.cs", 18)),
                new NUnitCaseResult(
                    "SampleTests.Fixture.Fail",
                    "Fail",
                    "Failed",
                    8.2,
                    "Expected: True\n  But was: False",
                    "at SampleTests.Fixture.Fail() in C:\\tests\\SampleTests.cs:line 42",
                    null,
                    Attachments:
                    [
                        new NUnitAttachment(
                            "failure-screenshot",
                            "image/png",
                            "C:\\temp\\failure.png",
                            null),
                    ]),
                new NUnitCaseResult(
                    "SampleTests.Fixture.Skip",
                    "Skip",
                    "Skipped",
                    0.0,
                    null,
                    null,
                    null,
                    SkipReason: "Not implemented yet"),
            ],
            GenerationId: SampleGenerationId);

        var message = BridgeMessage.Response(
            "3",
            JsonSerializer.SerializeToElement(response, NUnitJsonContext.Default.NUnitRunResponse));

        Assert.Equal(
            """
            {"type":"response","id":"3","result":{"run_id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","summary":{"passed":1,"failed":1,"skipped":0,"inconclusive":0,"errors":0,"cancelled":0},"cases":[{"id":"SampleTests.Fixture.Pass","name":"Pass","outcome":"Passed","duration_ms":12.5,"output":"sample stdout","parent_test_id":"SampleTests.Fixture","traits":[{"name":"Category","value":"Integration"}],"source":{"file":"C:\\tests\\SampleTests.cs","line":18}},{"id":"SampleTests.Fixture.Fail","name":"Fail","outcome":"Failed","duration_ms":8.2,"message":"Expected: True\n  But was: False","stack_trace":"at SampleTests.Fixture.Fail() in C:\\tests\\SampleTests.cs:line 42","attachments":[{"name":"failure-screenshot","content_type":"image/png","path":"C:\\temp\\failure.png"}]},{"id":"SampleTests.Fixture.Skip","name":"Skip","outcome":"Skipped","duration_ms":0,"skip_reason":"Not implemented yet"}],"generation_id":"gen-7f3c2a1b9e4d8c0f6a2b5d9e1c4f7a0b"},"isError":false}
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
                "live output",
                Attachments:
                [
                    new NUnitAttachment(
                        "trace",
                        "text/plain",
                        null,
                        "dHJhY2U="),
                ]));

        var message = BridgeMessage.Notification(
            NUnitProtocol.Progress,
            JsonSerializer.SerializeToElement(progress, NUnitJsonContext.Default.NUnitProgressEvent));

        Assert.Equal(
            """
            {"type":"notification","method":"nunit/progress","params":{"run_id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","case":{"id":"SampleTests.Fixture.Pass","name":"Pass","outcome":"Passed","duration_ms":12.5,"output":"live output","attachments":[{"name":"trace","content_type":"text/plain","base64":"dHJhY2U="}]}},"isError":false}
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
    public void DiscoveredTest_RoundTripsHierarchyTraitsSourceAndSkipReason()
    {
        var original = new NUnitDiscoveredTest(
            "SampleTests.Fixture.Skip",
            "Skip",
            "SampleTests.Fixture.Skip",
            ParentTestId: "SampleTests.Fixture",
            Traits: [new NUnitTrait("Category", "Integration")],
            Source: new NUnitSourceLocation("C:\\tests\\SampleTests.cs", 99),
            SkipReason: "Explicit test");

        var json = JsonSerializer.Serialize(original, NUnitJsonContext.Default.NUnitDiscoveredTest);
        var roundTrip = JsonSerializer.Deserialize(json, NUnitJsonContext.Default.NUnitDiscoveredTest);

        Assert.NotNull(roundTrip);
        Assert.Equal(original.Id, roundTrip.Id);
        Assert.Equal(original.ParentTestId, roundTrip.ParentTestId);
        Assert.Equal(original.SkipReason, roundTrip.SkipReason);
        Assert.NotNull(roundTrip.Traits);
        Assert.Single(roundTrip.Traits);
        Assert.Equal("Category", roundTrip.Traits[0].Name);
        Assert.Equal("Integration", roundTrip.Traits[0].Value);
        Assert.NotNull(roundTrip.Source);
        Assert.Equal("C:\\tests\\SampleTests.cs", roundTrip.Source.File);
        Assert.Equal(99, roundTrip.Source.Line);
    }

    [Fact]
    public void CaseResult_RoundTripsFailureDetailsAttachmentsAndGenerationMetadata()
    {
        var original = new NUnitCaseResult(
            "SampleTests.Fixture.Fail",
            "Fail",
            "Failed",
            8.2,
            "Expected: True\n  But was: False",
            "at SampleTests.Fixture.Fail() in C:\\tests\\SampleTests.cs:line 42",
            "captured output",
            ParentTestId: "SampleTests.Fixture",
            Traits: [new NUnitTrait("Category", "Integration")],
            Source: new NUnitSourceLocation("C:\\tests\\SampleTests.cs", 42),
            SkipReason: null,
            Attachments:
            [
                new NUnitAttachment("log", "text/plain", "C:\\temp\\fail.log", null),
            ]);

        var json = JsonSerializer.Serialize(original, NUnitJsonContext.Default.NUnitCaseResult);
        var roundTrip = JsonSerializer.Deserialize(json, NUnitJsonContext.Default.NUnitCaseResult);

        Assert.NotNull(roundTrip);
        Assert.Equal(original.Id, roundTrip.Id);
        Assert.Equal(original.Name, roundTrip.Name);
        Assert.Equal(original.Outcome, roundTrip.Outcome);
        Assert.Equal(original.DurationMs, roundTrip.DurationMs);
        Assert.Equal(original.Message, roundTrip.Message);
        Assert.Equal(original.StackTrace, roundTrip.StackTrace);
        Assert.Equal(original.Output, roundTrip.Output);
        Assert.Equal(original.ParentTestId, roundTrip.ParentTestId);
        Assert.NotNull(roundTrip.Attachments);
        Assert.Single(roundTrip.Attachments);
        Assert.Equal("log", roundTrip.Attachments[0].Name);
    }

    [Fact]
    public void DiscoverResponse_RoundTripsGenerationIdAndRuntimeDiagnostic()
    {
        var original = new NUnitDiscoverResponse(
            [new NUnitDiscoveredTest("id", "name", "full")],
            GenerationId: SampleGenerationId,
            RuntimeDiagnostic: new NUnitRuntimeDiagnostic("generation.retained", "Generation retained after unload."));

        var json = JsonSerializer.Serialize(original, NUnitJsonContext.Default.NUnitDiscoverResponse);
        var roundTrip = JsonSerializer.Deserialize(json, NUnitJsonContext.Default.NUnitDiscoverResponse);

        Assert.NotNull(roundTrip);
        Assert.Equal(SampleGenerationId, roundTrip.GenerationId);
        Assert.NotNull(roundTrip.RuntimeDiagnostic);
        Assert.Equal("generation.retained", roundTrip.RuntimeDiagnostic.Code);
        Assert.Equal("Generation retained after unload.", roundTrip.RuntimeDiagnostic.Message);
    }

    private static string SerializeBridgeMessage(BridgeMessage message) =>
        JsonSerializer.Serialize(message, IpcJsonContext.Default.BridgeMessage);
}
