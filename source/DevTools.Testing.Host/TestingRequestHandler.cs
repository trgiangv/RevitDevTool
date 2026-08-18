using System.Text.Json;
using DevTools.Ipc;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Host;

public static class TestingErrorCodes
{
    public const string InvalidRequest = "testing/invalid_request";
    public const string SessionPoisoned = "testing/session_poisoned";
    public const string ProviderFailed = "testing/provider_failed";
}

/// <summary>
/// Host-side handler for the framework-neutral <c>testing/*</c> protocol.
/// </summary>
public sealed class TestingRequestHandler : IBridgeRequestHandler, IBridgeNotificationPublisher
{
    private readonly TestingProviderRegistry _registry;
    private readonly string _host;
    private readonly string _hostVersion;
    private readonly TestingCancellationStateMachine _cancellation = new();
    private int _isBusy;

    public TestingRequestHandler(
        TestingProviderRegistry registry,
        string host,
        string hostVersion)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _hostVersion = hostVersion ?? throw new ArgumentNullException(nameof(hostVersion));
        SupportedMethods =
        [
            TestingProtocol.Hello,
            TestingProtocol.Run,
            TestingProtocol.Cancel,
        ];
    }

    public Action<string, JsonElement?>? NotificationSender { get; set; }

    public IReadOnlyCollection<string> SupportedMethods { get; }

    public TestingCancellationState CancellationState => _cancellation.State;

    public Task<BridgeMessage> HandleAsync(
        string requestId,
        string method,
        JsonElement? @params,
        CancellationToken ct = default)
    {
        if (string.Equals(method, TestingProtocol.Hello, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(HandleHello(requestId, @params));

        if (string.Equals(method, TestingProtocol.Run, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(HandleRun(requestId, @params, ct));

        if (string.Equals(method, TestingProtocol.Cancel, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(HandleCancel(requestId, @params));

        return Task.FromResult(BridgeMessage.Error(
            requestId,
            IpcErrorCodes.MethodNotFound,
            $"Unknown method: {method}"));
    }

    private BridgeMessage HandleHello(string requestId, JsonElement? @params)
    {
        if (!TryReadHello(@params, out var request, out var error))
            return Invalid(requestId, error);

        if (!TestingProtocol.IsCompatible(request!.ProtocolVersion))
            return TestingProtocol.CreateIncompatibleResponse(requestId, request.ProtocolVersion);

        if (string.IsNullOrWhiteSpace(request!.FrameworkId))
            return Invalid(requestId, "Framework ID is required.");

        string frameworkId;
        try
        {
            frameworkId = _registry.GetRequired(request.FrameworkId).FrameworkId;
        }
        catch (KeyNotFoundException ex)
        {
            return Invalid(requestId, ex.Message);
        }

        var response = new TestingHelloResponse(
            ProtocolVersion: TestingProtocol.CurrentVersion,
            FrameworkId: frameworkId,
            Host: _host,
            HostVersion: _hostVersion,
            ProcessId: Environment.ProcessId,
            IsBusy: Volatile.Read(ref _isBusy) != 0);

        return BridgeMessage.Response(
            requestId,
            JsonSerializer.SerializeToElement(response, TestingJsonContext.Default.TestingHelloResponse));
    }

    private BridgeMessage HandleRun(
        string requestId,
        JsonElement? @params,
        CancellationToken cancellationToken)
    {
        if (_cancellation.State == TestingCancellationState.Poisoned)
        {
            return BridgeMessage.Error(
                requestId,
                TestingErrorCodes.SessionPoisoned,
                "The testing session is poisoned.");
        }

        if (!TryReadRun(@params, out var request, out var error))
            return Invalid(requestId, error);

        IHostTestFrameworkProvider provider;
        try
        {
            provider = _registry.GetRequired(request!.FrameworkId);
        }
        catch (KeyNotFoundException ex)
        {
            return Invalid(requestId, ex.Message);
        }

        Interlocked.Exchange(ref _isBusy, 1);
        try
        {
            var sink = new HandlerEventSink(this);
            var response = provider.Run(request, sink, cancellationToken);
            if (response.CancellationState == TestingCancellationState.Poisoned)
                _cancellation.TryTransition(TestingCancellationState.Poisoned);
            else if (_cancellation.State == TestingCancellationState.Acknowledged)
                _cancellation.TryTransition(TestingCancellationState.Completed);

            return BridgeMessage.Response(
                requestId,
                JsonSerializer.SerializeToElement(response, TestingJsonContext.Default.TestingRunResponse));
        }
        catch (Exception ex)
        {
            if (_cancellation.State is TestingCancellationState.None)
                _cancellation.TryTransition(TestingCancellationState.Requested);
            if (_cancellation.State is TestingCancellationState.Requested)
                _cancellation.TryTransition(TestingCancellationState.Poisoned);
            else
                _cancellation.TryTransition(TestingCancellationState.Poisoned);

            return BridgeMessage.Error(requestId, TestingErrorCodes.ProviderFailed, ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _isBusy, 0);
        }
    }

    private BridgeMessage HandleCancel(string requestId, JsonElement? @params)
    {
        if (!TryReadCancel(@params, out var request, out var error))
            return Invalid(requestId, error);

        if (_cancellation.State == TestingCancellationState.None)
            _cancellation.Transition(TestingCancellationState.Requested);

        var acknowledged = _registry.Cancel(request!.RunId);

        if (acknowledged)
            _cancellation.TryTransition(TestingCancellationState.Acknowledged);

        return BridgeMessage.Response(requestId, null);
    }

    private static bool TryReadHello(
        JsonElement? @params,
        out TestingHelloRequest? request,
        out string error)
    {
        request = null;
        error = string.Empty;
        if (@params is null)
        {
            error = "Request params are required.";
            return false;
        }

        try
        {
            request = @params.Value.Deserialize(TestingJsonContext.Default.TestingHelloRequest);
            if (request is null)
            {
                error = "Empty hello request.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryReadRun(
        JsonElement? @params,
        out TestingRunRequest? request,
        out string error)
    {
        request = null;
        error = string.Empty;
        if (@params is null)
        {
            error = "Request params are required.";
            return false;
        }

        try
        {
            request = @params.Value.Deserialize(TestingJsonContext.Default.TestingRunRequest);
            if (request is null)
            {
                error = "Empty run request.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryReadCancel(
        JsonElement? @params,
        out TestingCancelRequest? request,
        out string error)
    {
        request = null;
        error = string.Empty;
        if (@params is null)
        {
            error = "Request params are required.";
            return false;
        }

        try
        {
            request = @params.Value.Deserialize(TestingJsonContext.Default.TestingCancelRequest);
            if (request is null)
            {
                error = "Empty cancel request.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static BridgeMessage Invalid(string requestId, string error) =>
        BridgeMessage.Error(requestId, TestingErrorCodes.InvalidRequest, error);

    private sealed class HandlerEventSink(TestingRequestHandler owner) : ITestingEventSink
    {
        public void Publish(TestingEvent testingEvent)
        {
            if (testingEvent.CancellationState == TestingCancellationState.Acknowledged)
                owner._cancellation.TryTransition(TestingCancellationState.Acknowledged);

            var sender = owner.NotificationSender;
            if (sender is null)
                return;

            sender(
                TestingProtocol.Progress,
                JsonSerializer.SerializeToElement(testingEvent, TestingJsonContext.Default.TestingEvent));
        }
    }
}
