using System.Text.Json;
using DevTools.Ipc;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Transport.Tests;

[TestClass]
public sealed class TestingProtocolGoldenTests
{
    static readonly Guid SampleRunId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [TestMethod]
    public void Hello_request_uses_testing_envelope()
    {
        var message = BridgeMessage.Request(
            "1",
            TestingProtocol.Hello,
            JsonSerializer.SerializeToElement(
                new TestHelloRequest(TestingProtocol.CurrentVersion, TestFrameworkId.NUnit),
                TestingJsonContext.Default.TestHelloRequest));

        Assert.AreEqual(
            """{"type":"request","id":"1","method":"testing/hello","params":{"protocol_version":2,"framework_id":"NUnit"},"isError":false}""",
            Serialize(message));
        Assert.DoesNotContain("testing/discover", Serialize(message), StringComparison.Ordinal);
    }

    [TestMethod]
    public void Run_request_round_trips_opaque_ids_and_selection_kind()
    {
        var request = CreateRunRequest(
            TestingProtocol.CurrentVersion,
            ["  spaced id  ", "xunit.v3://method/Theory(input: 1)/0"]);
        var json = JsonSerializer.Serialize(request, TestingJsonContext.Default.TestRunRequest);
        var roundTrip = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestRunRequest);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(request.Assembly.Path, roundTrip.Assembly.Path);
        Assert.AreEqual(TestSelectionKind.TestIds, roundTrip.Selection.Kind);
        Assert.AreEqual(request.Selection.TestIds[0], roundTrip.Selection.TestIds[0]);
        Assert.AreEqual(request.Selection.TestIds[1], roundTrip.Selection.TestIds[1]);
        Assert.Contains("\"protocol_version\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"framework_id\":\"NUnit\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("testing/discover", json, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Execute_round_trips_run_id_and_envelope_version()
    {
        var request = CreateRunRequest(
            TestingProtocol.CurrentVersion,
            ["  spaced id  ", "xunit.v3://method/Theory(input: 1)/0"]);
        var invocation = new TestRunExecute(
            TestingProtocol.CurrentVersion,
            new TestHostOptions("Revit", "2025", true, 60, 180, DebugParentPid: 4242),
            request);
        var json = JsonSerializer.Serialize(invocation, TestingJsonContext.Default.TestRunExecute);
        var roundTrip = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestRunExecute);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(TestingProtocol.CurrentVersion, roundTrip.ProtocolVersion);
        Assert.AreEqual(request.RunId, roundTrip.Run.RunId);
        Assert.AreEqual(request.Selection.TestIds[0], roundTrip.Run.Selection.TestIds[0]);
        Assert.AreEqual("Revit", roundTrip.Host.HostName);
        Assert.AreEqual(60, roundTrip.Host.PerTestTimeoutSeconds);
        Assert.AreEqual(4242, roundTrip.Host.DebugParentPid);
        Assert.Contains("\"run_id\":\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("testing/discover", json, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(TestCancellationState.None)]
    [DataRow(TestCancellationState.Requested)]
    [DataRow(TestCancellationState.Acknowledged)]
    [DataRow(TestCancellationState.Completed)]
    [DataRow(TestCancellationState.Poisoned)]
    public void Event_and_response_round_trip_every_cancellation_state(TestCancellationState state)
    {
        var result = new TestCaseResult(
            "opaque-id",
            "display",
            "Failed",
            8.5,
            "message",
            "stack",
            "output",
            new TestSourceLocation("C:\\tests\\Case.cs", 12),
            [new TestTrait("Category", "Host")],
            [new TestAttachment("C:\\temp\\log.txt", "trace")]);
        var testingEvent = new TestEvent(
            SampleRunId,
            TestEventKinds.Cancellation,
            result,
            state.ToString(),
            result.Attachments[0],
            state);
        var response = new TestRunResponse(
            SampleRunId,
            TestFrameworkId.TUnit,
            "gen-1",
            [result],
            state,
            "future-provider/runtime_restart_required",
            "restart");

        var eventJson = JsonSerializer.Serialize(testingEvent, TestingJsonContext.Default.TestEvent);
        var eventRoundTrip = JsonSerializer.Deserialize(eventJson, TestingJsonContext.Default.TestEvent);
        var responseJson = JsonSerializer.Serialize(response, TestingJsonContext.Default.TestRunResponse);
        var responseRoundTrip = JsonSerializer.Deserialize(responseJson, TestingJsonContext.Default.TestRunResponse);

        Assert.IsNotNull(eventRoundTrip);
        Assert.AreEqual(state, eventRoundTrip.CancellationState);
        Assert.AreEqual("opaque-id", eventRoundTrip.Case?.TestId);
        Assert.IsNotNull(responseRoundTrip);
        Assert.AreEqual(state, responseRoundTrip.CancellationState);
        Assert.AreEqual("gen-1", responseRoundTrip.GenerationId);
        Assert.AreEqual("future-provider/runtime_restart_required", responseRoundTrip.DiagnosticCode);
        Assert.Contains("cancellation_state", eventJson, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Protocol_mismatch_rejects_version_1()
    {
        Assert.IsFalse(TestingProtocol.IsCompatible(1));
        Assert.IsTrue(TestingProtocol.IsCompatible(TestingProtocol.CurrentVersion));

        var error = TestingProtocol.CreateIncompatibleResponse("9", 1);
        var json = Serialize(error);
        Assert.Contains("testing/protocol_incompatible", json, StringComparison.Ordinal);
        Assert.Contains("\"requested\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"expected\":2", json, StringComparison.Ordinal);
        Assert.DoesNotContain("nunit/protocol_incompatible", json, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Transport_has_no_discover_endpoint()
    {
#pragma warning disable MSTEST0032 // const endpoint names document the transport contract.
        Assert.AreEqual("testing/hello", TestingProtocol.Hello);
        Assert.AreEqual("testing/run", TestingProtocol.Run);
        Assert.AreEqual("testing/cancel", TestingProtocol.Cancel);
        Assert.AreEqual("testing/progress", TestingProtocol.Progress);
#pragma warning restore MSTEST0032

        var directory = Path.Combine(FindRepositoryRoot(), "source", "DevTools.Testing.Transport");
        foreach (var path in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("testing/discover", text, StringComparison.Ordinal);
        }

        Assert.IsNull(typeof(ITestRunnerTransport).GetMethod("Discover"));
    }

    static TestRunRequest CreateRunRequest(int protocolVersion, IReadOnlyList<string> testIds) =>
        new(
            protocolVersion,
            SampleRunId,
            TestFrameworkId.NUnit,
            new TestAssemblyReference(@"C:\tests\Sample.dll"),
            TestSelection.FromTestIds(testIds));

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
