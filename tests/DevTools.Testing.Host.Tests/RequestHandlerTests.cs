using System.Text.Json;
using DevTools.Ipc;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Host.Tests;

public sealed class RequestHandlerTests
{
    [Fact]
    public async Task Hello_uses_testing_envelope()
    {
        var handler = CreateHandler(out _);
        var response = await Handle(handler,
            "1",
            TestingProtocol.Hello,
            JsonSerializer.SerializeToElement(
                new TestHelloRequest(TestingProtocol.CurrentVersion, TestFrameworkId.NUnit),
                TestingJsonContext.Default.TestHelloRequest));

        Assert.False(response.IsError);
        var hello = response.Result!.Value.Deserialize(TestingJsonContext.Default.TestHelloResponse);
        Assert.Equal(TestFrameworkId.NUnit, hello!.FrameworkId);
        Assert.Equal("Revit", hello.Host);
    }

    [Fact]
    public async Task Legacy_nunit_hello_is_not_routed_by_the_generic_handler()
    {
        var handler = CreateHandler(out _);
        using var document = JsonDocument.Parse("""{"protocol_version":2}""");
        var response = await Handle(handler,"1", "nunit/hello", document.RootElement);

        Assert.True(response.IsError);
        Assert.Equal(IpcErrorCodes.MethodNotFound, response.ErrorDetail!.Code);
    }

    [Fact]
    public async Task Hello_does_not_default_a_missing_provider_id()
    {
        var handler = new DotnetTestRequestHandler(
            new TestingProviderRegistry([new FakeProvider(TestFrameworkId.NUnit)]),
            "Revit",
            "2025");
        using var document = JsonDocument.Parse("""{"protocol_version":2,"framework_id":""}""");

        var response = await Handle(handler, "1", TestingProtocol.Hello, document.RootElement);

        Assert.True(response.IsError);
        Assert.Equal(TestingErrorCodes.InvalidRequest, response.ErrorDetail!.Code);
    }

    [Fact]
    public async Task Discover_methods_are_rejected()
    {
        var handler = CreateHandler(out _);
        var testing = await Handle(handler, "2", "testing/discover", null);

        Assert.True(testing.IsError);
        Assert.Equal(IpcErrorCodes.MethodNotFound, testing.ErrorDetail!.Code);
        Assert.DoesNotContain("testing/discover", handler.SupportedMethods);
    }

    [Fact]
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

        Assert.True(response.IsError);
        Assert.Equal(IpcErrorCodes.MethodNotFound, response.ErrorDetail!.Code);
        Assert.Null(seen);
    }

    [Fact]
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

        Assert.True(failed.IsError);
        Assert.Equal(TestingErrorCodes.InvalidRequest, failed.ErrorDetail!.Code);
        Assert.NotEqual(TestCancellationState.Poisoned, handler.CancellationState);

        provider.RunException = null;
        var run = await Handle(
            handler,
            "2",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest));
        Assert.False(run.IsError);
    }

    [Fact]
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

        Assert.True(failed.IsError);
        Assert.Equal(TestingErrorCodes.ProviderFailed, failed.ErrorDetail!.Code);
        Assert.Equal(TestCancellationState.Poisoned, handler.CancellationState);

        var poisoned = await Handle(
            handler,
            "2",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest));

        Assert.True(poisoned.IsError);
        Assert.Equal(TestingErrorCodes.SessionPoisoned, poisoned.ErrorDetail!.Code);
    }

    [Fact]
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
        Assert.Equal(TestCancellationState.Poisoned, handler.CancellationState);

        provider.RunException = null;
        var hello = await Handle(
            handler,
            "2",
            TestingProtocol.Hello,
            JsonSerializer.SerializeToElement(
                new TestHelloRequest(TestingProtocol.CurrentVersion, TestFrameworkId.NUnit),
                TestingJsonContext.Default.TestHelloRequest));
        Assert.False(hello.IsError);
        Assert.Equal(TestCancellationState.None, handler.CancellationState);

        var run = await Handle(
            handler,
            "3",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest));
        Assert.False(run.IsError);
    }

    [Fact]
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

        Assert.True(cancelled.IsError);
        Assert.Equal(IpcErrorCodes.InternalError, cancelled.ErrorDetail!.Code);
        Assert.NotEqual(TestCancellationState.Poisoned, handler.CancellationState);

        provider.RunException = null;
        var run = await Handle(
            handler,
            "2",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest(),
                TestingJsonContext.Default.TestRunRequest));
        Assert.False(run.IsError);
    }

    [Fact]
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

        Assert.False(response.IsError);
        Assert.Equal(runId, cancelled);
        Assert.Equal(TestCancellationState.Acknowledged, handler.CancellationState);
    }

    [Fact]
    public void Supported_methods_are_testing_only()
    {
        var handler = new DotnetTestRequestHandler(
            new TestingProviderRegistry([new FakeProvider(TestFrameworkId.NUnit)]),
            "Revit",
            "2025");

        Assert.Equal(
            new[] { TestingProtocol.Hello, TestingProtocol.Run, TestingProtocol.Cancel },
            handler.SupportedMethods.ToArray());
    }

    static DotnetTestRequestHandler CreateHandler(out FakeProvider provider)
    {
        provider = new FakeProvider(TestFrameworkId.NUnit);
        return new DotnetTestRequestHandler(
            new TestingProviderRegistry([provider]),
            "Revit",
            "2025");
    }

    static TestRunRequest CreateRunRequest() =>
        new(
            TestingProtocol.CurrentVersion,
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            TestFrameworkId.NUnit,
            new TestAssemblyReference(@"C:\tests\Sample.dll"),
            TestSelection.FromTestIds(["id-1"]));

    static Task<BridgeMessage> Handle(
        DotnetTestRequestHandler handler,
        string requestId,
        string method,
        JsonElement? @params) =>
        handler.HandleAsync(requestId, method, @params, TestContext.Current.CancellationToken);
}
