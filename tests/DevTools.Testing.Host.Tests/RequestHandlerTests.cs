using DevTools.Ipc;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Transport;
using System.Text.Json;

namespace DevTools.Testing.Host.Tests;

[TestClass]
public sealed class RequestHandlerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Hello_uses_testing_envelope()
    {
        var handler = CreateHandler(out _);
        var response = await Hello(handler, TestingProtocol.Sender);

        Assert.IsFalse(response.IsError);
        var hello = response.Result!.Value.Deserialize(TestingJsonContext.Default.TestHelloResponse)!;
        Assert.AreEqual(TestFrameworkId.NUnit, hello.FrameworkId);
        Assert.AreEqual("Revit", hello.Host);
        Assert.AreEqual(TestingProtocol.Sender, hello.Sender);
        Assert.AreEqual(TestingProtocol.RuntimeRange, hello.TestingRuntime);
    }

    [TestMethod]
    public async Task Hello_from_a_newer_compatible_runner_is_accepted()
    {
        var handler = CreateHandler(out _);
        var response = await Hello(handler, Sender("9.0.0", TestingProtocol.CurrentVersion + 1, TestingProtocol.MinVersion));

        Assert.IsFalse(response.IsError);
    }

    [TestMethod]
    public async Task Hello_from_a_runner_above_the_host_range_says_to_restart_the_host()
    {
        var handler = CreateHandler(out _);
        var response = await Hello(handler, Sender("9.0.0", TestingProtocol.CurrentVersion + 1, TestingProtocol.CurrentVersion + 1));

        Assert.IsTrue(response.IsError);
        Assert.AreEqual(TestingProtocol.IncompatibleCode, response.ErrorDetail!.Code);
        Assert.Contains("DevTools.TestRunner 9.0.0 is not compatible with DevTool add-in", response.ErrorMessage!, StringComparison.Ordinal);
        Assert.EndsWith(TestingProtocol.RestartHostMessage, response.ErrorMessage!, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Hello_without_a_sender_says_to_update_RevitDevTool()
    {
        var handler = CreateHandler(out _);
        using var document = JsonDocument.Parse("""{"protocol_version":2,"framework_id":"NUnit"}""");
        var response = await Handle(handler, "1", TestingProtocol.Hello, document.RootElement);

        Assert.IsTrue(response.IsError);
        Assert.AreEqual(TestingProtocol.IncompatibleCode, response.ErrorDetail!.Code);
        Assert.EndsWith(TestingProtocol.UpdateProductMessage, response.ErrorMessage!, StringComparison.Ordinal);
    }
    [TestMethod]
    public async Task Legacy_nunit_hello_is_not_routed_by_the_generic_handler()
    {
        var handler = CreateHandler(out _);
        using var document = JsonDocument.Parse("""{"framework_id":"NUnit"}""");
        var response = await Handle(handler, "1", "nunit/hello", document.RootElement);

        Assert.IsTrue(response.IsError);
        Assert.AreEqual(IpcErrorCodes.MethodNotFound, response.ErrorDetail!.Code);
    }

    [TestMethod]
    public async Task Hello_does_not_default_a_missing_provider_id()
    {
        var handler = new TestingRequestHandler(
            new TestingProviderRegistry([new FakeProvider(TestFrameworkId.NUnit)]),
            "Revit",
            "2025");
        using var document = JsonDocument.Parse("""{"framework_id":""}""");

        var response = await Handle(handler, "1", TestingProtocol.Hello, document.RootElement);

        Assert.IsTrue(response.IsError);
        Assert.AreEqual(TestingErrorCodes.InvalidRequest, response.ErrorDetail!.Code);
    }

    [TestMethod]
    public async Task Discover_methods_are_rejected()
    {
        var handler = CreateHandler(out _);
        var testing = await Handle(handler, "2", "testing/discover", null);

        Assert.IsTrue(testing.IsError);
        Assert.AreEqual(IpcErrorCodes.MethodNotFound, testing.ErrorDetail!.Code);
        Assert.DoesNotContain("testing/discover", handler.SupportedMethods);
    }

    [TestMethod]
    public async Task Legacy_nunit_run_is_not_routed_by_the_generic_handler()
    {
        var handler = CreateHandler(out var provider);
        TestRunRequest? seen = null;
        provider.OnRun = request =>
        {
            seen = request;
            return new TestRunResponse(
                request.RunId,
                request.FrameworkId,
                "gen",
                [],
                TestCancellationState.None,
                null,
                null);
        };

        var request = CreateRunRequest();
        var response = await Handle(handler,
            "1",
            "provider/run",
            JsonSerializer.SerializeToElement(request, TestingJsonContext.Default.TestRunRequest));

        Assert.IsTrue(response.IsError);
        Assert.AreEqual(IpcErrorCodes.MethodNotFound, response.ErrorDetail!.Code);
        Assert.IsNull(seen);
    }

    [TestMethod]
    public async Task ArgumentException_is_invalid_request_and_does_not_poison()
    {
        var handler = CreateHandler(out var provider);
        provider.RunException = new ArgumentException("bad filter");

        var failed = await Handle(
            handler,
            "1",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest));

        Assert.IsTrue(failed.IsError);
        Assert.AreEqual(TestingErrorCodes.InvalidRequest, failed.ErrorDetail!.Code);
        Assert.AreNotEqual(TestCancellationState.Poisoned, handler.CancellationState);

        provider.RunException = null;
        var run = await Handle(
            handler,
            "2",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest));
        Assert.IsFalse(run.IsError);
    }

    [TestMethod]
    public async Task Runtime_incompatibility_is_protocol_incompatible_and_does_not_poison()
    {
        var handler = CreateHandler(out var provider);
        const string sentence = "NUnit is older than what the adapter needs.";
        provider.RunException = new TestingRuntimeIncompatibleException(sentence);

        var failed = await Handle(
            handler,
            "1",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest));

        Assert.IsTrue(failed.IsError);
        Assert.AreEqual(TestingErrorCodes.ProtocolIncompatible, failed.ErrorDetail!.Code);
        Assert.AreEqual(sentence, failed.ErrorMessage);
        Assert.AreNotEqual(TestCancellationState.Poisoned, handler.CancellationState);

        provider.RunException = null;
        provider.OnRun = request => new TestRunResponse(request.RunId, request.FrameworkId, "gen", [], TestCancellationState.None, null, null);
        var next = await Handle(
            handler,
            "2",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest));

        Assert.IsFalse(next.IsError);
    }

    [TestMethod]
    public async Task Provider_exception_poisons_session()
    {
        var handler = CreateHandler(out var provider);
        provider.RunException = new InvalidOperationException("provider crashed");

        var failed = await Handle(
            handler,
            "1",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest));

        Assert.IsTrue(failed.IsError);
        Assert.AreEqual(TestingErrorCodes.ProviderFailed, failed.ErrorDetail!.Code);
        Assert.AreEqual(TestCancellationState.Poisoned, handler.CancellationState);

        var poisoned = await Handle(
            handler,
            "2",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest));

        Assert.IsTrue(poisoned.IsError);
        Assert.AreEqual(TestingErrorCodes.SessionPoisoned, poisoned.ErrorDetail!.Code);
    }

    [TestMethod]
    public async Task Hello_resets_a_poisoned_session_for_the_next_run()
    {
        var handler = CreateHandler(out var provider);
        provider.RunException = new InvalidOperationException("provider crashed");

        await Handle(
            handler,
            "1",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest));
        Assert.AreEqual(TestCancellationState.Poisoned, handler.CancellationState);

        provider.RunException = null;
        var hello = await Handle(
            handler,
            "2",
            TestingProtocol.Hello,
            JsonSerializer.SerializeToElement(
                new TestHelloRequest(TestFrameworkId.NUnit, TestingProtocol.Sender),
                TestingJsonContext.Default.TestHelloRequest));
        Assert.IsFalse(hello.IsError);
        Assert.AreEqual(TestCancellationState.None, handler.CancellationState);

        var run = await Handle(
            handler,
            "3",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest));
        Assert.IsFalse(run.IsError);
    }

    [TestMethod]
    public async Task Run_client_disconnect_does_not_poison_the_session()
    {
        var handler = CreateHandler(out var provider);
        provider.RunException = new OperationCanceledException();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cancelled = await handler.HandleAsync(
            "1",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest),
            cts.Token);

        Assert.IsTrue(cancelled.IsError);
        Assert.AreEqual(IpcErrorCodes.InternalError, cancelled.ErrorDetail!.Code);
        Assert.AreNotEqual(TestCancellationState.Poisoned, handler.CancellationState);

        provider.RunException = null;
        var run = await Handle(
            handler,
            "2",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest));
        Assert.IsFalse(run.IsError);
    }

    [TestMethod]
    public async Task Cancel_acknowledges_through_provider()
    {
        var handler = CreateHandler(out var provider);
        var runId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid? cancelled = null;
        provider.OnCancel = id =>
        {
            cancelled = id;
            return true;
        };

        var response = await Handle(handler,
            "1",
            TestingProtocol.Cancel,
            JsonSerializer.SerializeToElement(
                new TestCancelRequest(runId),
                TestingJsonContext.Default.TestCancelRequest));

        Assert.IsFalse(response.IsError);
        Assert.AreEqual(runId, cancelled);
        Assert.AreEqual(TestCancellationState.Acknowledged, handler.CancellationState);
    }

    [TestMethod]
    public void Supported_methods_are_testing_only()
    {
        var handler = new TestingRequestHandler(
            new TestingProviderRegistry([new FakeProvider(TestFrameworkId.NUnit)]),
            "Revit",
            "2025");

        Assert.AreSequenceEqual(
            new[] { TestingProtocol.Hello, TestingProtocol.Run, TestingProtocol.Cancel },
            handler.SupportedMethods.ToArray());
    }

    static TestingRequestHandler CreateHandler(out FakeProvider provider)
    {
        provider = new FakeProvider(TestFrameworkId.NUnit);
        return new TestingRequestHandler(
            new TestingProviderRegistry([provider]),
            "Revit",
            "2025");
    }

    static TestRunRequest CreateRunRequest() =>
        new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            TestFrameworkId.NUnit,
            new TestAssemblyReference(@"C:\tests\Sample.dll"),
            TestSelection.FromTestIds(["id-1"]));

    static ProtocolSender Sender(string version, int current, int min) => new(version, new ProtocolRange(current, min));

    static Task<BridgeMessage> Hello(TestingRequestHandler handler, ProtocolSender sender) =>
        Handle(handler,
            "1",
            TestingProtocol.Hello,
            JsonSerializer.SerializeToElement(
                new TestHelloRequest(TestFrameworkId.NUnit, sender),
                TestingJsonContext.Default.TestHelloRequest));

    static Task<BridgeMessage> Handle(
        TestingRequestHandler handler,
        string requestId,
        string method,
        JsonElement? @params) =>
        handler.HandleAsync(requestId, method, @params, CancellationToken.None);
}
