using System.Text.Json;
using DevTools.Ipc;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Transport.Tests;

public sealed class TestingProtocolGoldenTests
{
    static readonly Guid SampleRunId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void Hello_request_uses_testing_envelope()
    {
        var message = BridgeMessage.Request(
            "1",
            TestingProtocol.Hello,
            JsonSerializer.SerializeToElement(
                new TestingHelloRequest(TestingProtocol.CurrentVersion, TestingFrameworkIds.NUnit),
                TestingJsonContext.Default.TestingHelloRequest));

        Assert.Equal(
            """{"type":"request","id":"1","method":"testing/hello","params":{"protocol_version":2,"framework_id":"nunit"},"isError":false}""",
            Serialize(message));
        Assert.DoesNotContain("testing/discover", Serialize(message), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_request_round_trips_assembly_identity_and_opaque_ids()
    {
        var request = CreateRunRequest(
            TestingProtocol.CurrentVersion,
            ["  spaced id  ", "xunit.v3://method/Theory(input: 1)/0"]);
        var json = JsonSerializer.Serialize(request, TestingJsonContext.Default.TestingRunRequest);
        var roundTrip = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestingRunRequest);

        Assert.NotNull(roundTrip);
        Assert.Equal(request.Assembly.Path, roundTrip.Assembly.Path);
        Assert.Equal(request.Assembly.TargetFramework, roundTrip.Assembly.TargetFramework);
        Assert.Equal(request.Assembly.ContentHash, roundTrip.Assembly.ContentHash);
        Assert.Equal(request.Selection.TestIds[0], roundTrip.Selection.TestIds[0]);
        Assert.Equal(request.Selection.TestIds[1], roundTrip.Selection.TestIds[1]);
        Assert.Contains("\"protocol_version\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"framework_id\":\"nunit\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("testing/discover", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TestingCancellationState.None)]
    [InlineData(TestingCancellationState.Requested)]
    [InlineData(TestingCancellationState.Acknowledged)]
    [InlineData(TestingCancellationState.Completed)]
    [InlineData(TestingCancellationState.Poisoned)]
    public void Event_and_response_round_trip_every_cancellation_state(TestingCancellationState state)
    {
        var result = new TestingCaseResult(
            "opaque-id",
            "display",
            "Failed",
            8.5,
            "message",
            "stack",
            "output",
            new TestingSourceLocation("C:\\tests\\Case.cs", 12),
            [new TestingTrait("Category", "Host")],
            [new TestingAttachment("C:\\temp\\log.txt", "trace")]);
        var testingEvent = new TestingEvent(
            SampleRunId,
            TestingEventKinds.Cancellation,
            result,
            state.ToString(),
            result.Attachments[0],
            state);
        var response = new TestingRunResponse(
            SampleRunId,
            "future-provider",
            "gen-1",
            [result],
            state,
            "future-provider/runtime_restart_required",
            "restart");

        var eventJson = JsonSerializer.Serialize(testingEvent, TestingJsonContext.Default.TestingEvent);
        var eventRoundTrip = JsonSerializer.Deserialize(eventJson, TestingJsonContext.Default.TestingEvent);
        var responseJson = JsonSerializer.Serialize(response, TestingJsonContext.Default.TestingRunResponse);
        var responseRoundTrip = JsonSerializer.Deserialize(responseJson, TestingJsonContext.Default.TestingRunResponse);

        Assert.NotNull(eventRoundTrip);
        Assert.Equal(state, eventRoundTrip.CancellationState);
        Assert.Equal("opaque-id", eventRoundTrip.Case?.TestId);
        Assert.NotNull(responseRoundTrip);
        Assert.Equal(state, responseRoundTrip.CancellationState);
        Assert.Equal("gen-1", responseRoundTrip.GenerationId);
        Assert.Equal("future-provider/runtime_restart_required", responseRoundTrip.DiagnosticCode);
        Assert.Contains("cancellation_state", eventJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Protocol_mismatch_rejects_version_1()
    {
        Assert.False(TestingProtocolBridge.IsCompatible(1));
        Assert.True(TestingProtocolBridge.IsCompatible(TestingProtocol.CurrentVersion));

        var error = TestingProtocolBridge.CreateIncompatibleResponse("9", 1);
        var json = Serialize(error);
        Assert.Contains("testing/protocol_incompatible", json, StringComparison.Ordinal);
        Assert.Contains("\"requested\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"expected\":2", json, StringComparison.Ordinal);
        Assert.DoesNotContain("nunit/protocol_incompatible", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Transport_has_no_discover_endpoint()
    {
        Assert.Equal("testing/hello", TestingProtocol.Hello);
        Assert.Equal("testing/run", TestingProtocol.Run);
        Assert.Equal("testing/cancel", TestingProtocol.Cancel);
        Assert.Equal("testing/progress", TestingProtocol.Progress);

        var directory = Path.Combine(FindRepositoryRoot(), "source", "DevTools.Testing.Transport");
        foreach (var path in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("testing/discover", text, StringComparison.Ordinal);
        }

        Assert.Null(typeof(ITestRunnerTransport).GetMethod("Discover"));
    }

    static TestingRunRequest CreateRunRequest(int protocolVersion, IReadOnlyList<string> testIds) =>
        new(
            protocolVersion,
            SampleRunId,
            TestingFrameworkIds.NUnit,
            new TestingAssemblyReference(@"C:\tests\Sample.dll", "net10.0-windows", "abc123"),
            new TestingSelection(testIds, ProviderPayload: "opaque-bytes"),
            new Dictionary<string, string> { ["preEnumerateTheories"] = "false" });

    static string Serialize(BridgeMessage message) =>
        JsonSerializer.Serialize(message, IpcJsonContext.Default.BridgeMessage);

    static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
