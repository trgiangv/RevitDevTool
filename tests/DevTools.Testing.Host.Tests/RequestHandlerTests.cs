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
                new TestingHelloRequest(TestingProtocol.CurrentVersion, "provider.example"),
                TestingJsonContext.Default.TestingHelloRequest));

        Assert.False(response.IsError);
        var hello = response.Result!.Value.Deserialize(TestingJsonContext.Default.TestingHelloResponse);
        Assert.Equal("provider.example", hello!.FrameworkId);
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
        var handler = new TestingRequestHandler(
            new TestingProviderRegistry([new FakeProvider("nunit")]),
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
        Assert.Equal(TestingErrorCodes.NoDiscovery, testing.ErrorDetail!.Code);
        Assert.DoesNotContain("testing/discover", handler.SupportedMethods);
    }

    [Fact]
    public async Task Legacy_nunit_run_is_not_routed_by_the_generic_handler()
    {
        var handler = CreateHandler(out var provider);
        TestingRunRequest? seen = null;
        provider.OnRun = request =>
        {
            seen = request;
            return new TestingRunResponse(
                request.RunId,
                request.FrameworkId,
                "gen",
                [],
                TestingCancellationState.None,
                null,
                null);
        };

        var request = CreateRunRequest("provider.example");
        var response = await Handle(handler,
            "1",
            "provider/run",
            JsonSerializer.SerializeToElement(request, TestingJsonContext.Default.TestingRunRequest));

        Assert.True(response.IsError);
        Assert.Equal(IpcErrorCodes.MethodNotFound, response.ErrorDetail!.Code);
        Assert.Null(seen);
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
                CreateRunRequest("provider.example"),
                TestingJsonContext.Default.TestingRunRequest));

        Assert.True(failed.IsError);
        Assert.Equal(TestingErrorCodes.ProviderFailed, failed.ErrorDetail!.Code);
        Assert.Equal(TestingCancellationState.Poisoned, handler.CancellationState);

        var poisoned = await Handle(
            handler,
            "2",
            TestingProtocol.Run,
            JsonSerializer.SerializeToElement(
                CreateRunRequest("provider.example"),
                TestingJsonContext.Default.TestingRunRequest));

        Assert.True(poisoned.IsError);
        Assert.Equal(TestingErrorCodes.SessionPoisoned, poisoned.ErrorDetail!.Code);
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
                new TestingCancelRequest(runId),
                TestingJsonContext.Default.TestingCancelRequest));

        Assert.False(response.IsError);
        Assert.Equal(runId, cancelled);
        Assert.Equal(TestingCancellationState.Acknowledged, handler.CancellationState);
    }

    [Fact]
    public void Supported_methods_are_testing_only()
    {
        var handler = new TestingRequestHandler(
            new TestingProviderRegistry([new FakeProvider("provider.example")]),
            "Revit",
            "2025");

        Assert.Equal(
            new[] { TestingProtocol.Hello, TestingProtocol.Run, TestingProtocol.Cancel },
            handler.SupportedMethods.ToArray());
    }

    static TestingRequestHandler CreateHandler(out FakeProvider provider)
    {
        provider = new FakeProvider("provider.example");
        return new TestingRequestHandler(
            new TestingProviderRegistry([provider]),
            "Revit",
            "2025");
    }

    static TestingRunRequest CreateRunRequest(string frameworkId) =>
        new(
            TestingProtocol.CurrentVersion,
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            frameworkId,
            new TestingAssemblyReference(@"C:\tests\Sample.dll", "net10.0-windows", "hash"),
            new TestingSelection(["id-1"]),
            new Dictionary<string, string>());

    static Task<BridgeMessage> Handle(
        TestingRequestHandler handler,
        string requestId,
        string method,
        JsonElement? @params) =>
        handler.HandleAsync(requestId, method, @params, TestContext.Current.CancellationToken);
}
