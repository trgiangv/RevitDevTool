using System.Text;
using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Testing;
using DevTools.Ipc;

namespace RevitDevTool.Execution.Tests;

public sealed class PytestBridgeCompatibilityTests
{
    [Fact]
    public async Task TestsRun_UsesTheDirectPipeEnvelopeAndStablePytestWireKeys()
    {
        var request = new PytestRunRequest(
            "C:\\workspace",
            "C:\\workspace\\tests",
            ["tests/test_sample.py::test_runs"],
            ["-q"]);
        var envelope = BridgeMessage.Request(
            "request-42",
            PytestBridgeMethods.TestsRun,
            JsonSerializer.SerializeToElement(request));

        await using var stream = new MemoryStream();
        using (var connection = new BridgePipeConnection(stream))
            await connection.WriteAsync(envelope, TestContext.Current.CancellationToken);

        var framed = stream.ToArray();
        var frameLength = BitConverter.ToInt32(framed, 0);
        var body = Encoding.UTF8.GetString(framed, 4, frameLength);

        Assert.Equal("DevTools_Revit_2025_123", HostPipeName.Format("Revit", "2025", 123));
        Assert.Equal(framed.Length - 4, frameLength);
        Assert.Equal((byte)frameLength, framed[0]);
        Assert.Equal((byte)(frameLength >> 8), framed[1]);
        Assert.Equal((byte)(frameLength >> 16), framed[2]);
        Assert.Equal((byte)(frameLength >> 24), framed[3]);
        Assert.Contains("\"type\":\"request\"", body, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"request-42\"", body, StringComparison.Ordinal);
        Assert.Contains("\"method\":\"tests/run\"", body, StringComparison.Ordinal);
        Assert.Contains("\"workspace_root\"", body, StringComparison.Ordinal);
        Assert.Contains("\"test_root\"", body, StringComparison.Ordinal);
        Assert.Contains("\"nodeids\"", body, StringComparison.Ordinal);
        Assert.Contains("\"pytest_args\"", body, StringComparison.Ordinal);

        var result = new PytestRunResponse(
            0,
            new PytestSummary(1, 0, 0, 0, 0, 0),
            [new PytestCaseResult("tests/test_sample.py::test_runs", "passed", "call", 5, "", "", "ok", "")],
            [],
            "C:\\workspace");
        var responseJson = JsonSerializer.Serialize(result);

        Assert.Contains("\"exit_code\"", responseJson, StringComparison.Ordinal);
        Assert.Contains("\"summary\"", responseJson, StringComparison.Ordinal);
        Assert.Contains("\"results\"", responseJson, StringComparison.Ordinal);
        Assert.Contains("\"collection_errors\"", responseJson, StringComparison.Ordinal);
        Assert.Contains("\"rootdir\"", responseJson, StringComparison.Ordinal);
        Assert.Contains($"\"{PytestWireProperties.Message}\":\"ok\"", responseJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestsRun_PreparesThenSuppressesThenExecutesOnTheHostAndStreamsProgressBeforeTheResponse()
    {
        var events = new List<string>();
        var previousMode = ExecutionGuardContext.Mode;
        try
        {
            ExecutionGuardContext.Mode = ExecutionGuardMode.Passthrough;
            var handler = new PytestRequestHandler(
                new RecordingHostContextExecutor(events),
                new RecordingDependencyService(events),
                new RecordingExecutionService(events))
            {
                NotifySender = (method, data) =>
                {
                    events.Add("progress");
                    Assert.Equal(PytestBridgeMethods.NotifyTestProgress, method);
                    Assert.Equal("tests/test_sample.py::test_runs", data?.GetProperty("nodeid").GetString());
                },
            };

            var response = await handler.HandleAsync(
                "request-42",
                PytestBridgeMethods.TestsRun,
                RunRequestParameters(),
                TestContext.Current.CancellationToken);

            Assert.Equal(["prepare", "suppress", "host", "run", "progress"], events);
            Assert.Equal(BridgeMessage.TypeResponse, response.Type);
            Assert.Equal("request-42", response.Id);
            Assert.Equal(0, response.Result?.GetProperty("exit_code").GetInt32());
        }
        finally
        {
            ExecutionGuardContext.Mode = previousMode;
        }
    }

    [Fact]
    public async Task TestsRun_PreparationFailureReturnsAnErrorResponseWithoutEnteringHostContext()
    {
        var events = new List<string>();
        var handler = new PytestRequestHandler(
            new RecordingHostContextExecutor(events),
            new RecordingDependencyService(events, new InvalidOperationException("Pixi unavailable")),
            new RecordingExecutionService(events));

        var response = await handler.HandleAsync(
            "request-43",
            PytestBridgeMethods.TestsRun,
            RunRequestParameters(),
            TestContext.Current.CancellationToken);

        Assert.Equal(["prepare"], events);
        Assert.Equal("request-43", response.Id);
        Assert.Equal(1, response.Result?.GetProperty("exit_code").GetInt32());
        Assert.Contains("[prepare] Failed to prepare pytest session.", response.Result?.GetProperty("collection_errors")[0].GetProperty("message").GetString());
    }

    private static JsonElement RunRequestParameters()
        => JsonSerializer.SerializeToElement(new PytestRunRequest(
            "C:\\workspace",
            "C:\\workspace\\tests",
            ["tests/test_sample.py::test_runs"],
            ["-q"]));

    private sealed class RecordingHostContextExecutor(IList<string> events) : IHostContextExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default)
        {
            events.Add(ExecutionGuardContext.Mode == ExecutionGuardMode.Suppress ? "suppress" : "unexpected-mode");
            events.Add("host");
            return Task.FromResult(handler());
        }

        public Task ExecuteAsync(Action action, CancellationToken token = default)
        {
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDependencyService(IList<string> events, Exception? failure = null) : PytestDependencyService(null!)
    {
        public override Task PrepareRunAsync(PytestRunRequest request, CancellationToken cancellationToken = default)
        {
            events.Add("prepare");
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }
    }

    private sealed class RecordingExecutionService(IList<string> events) : PytestExecutionService(null!)
    {
        public override PytestRunResponse Run(PytestRunRequest request, Action<string>? progressCallback = null)
        {
            events.Add("run");
            progressCallback?.Invoke(JsonSerializer.Serialize(new PytestCaseResult(
                "tests/test_sample.py::test_runs", "passed", "call", 5, "", "", "ok", "")));
            return new PytestRunResponse(0, new PytestSummary(1, 0, 0, 0, 0, 0), [], [], "C:\\workspace");
        }
    }
}
