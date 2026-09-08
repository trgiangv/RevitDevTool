using System.Text.Json;
using DevTools.Ipc;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Transport;
// ReSharper disable RedundantSuppressNullableWarningExpression

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
public sealed class DotnetTestRequestHandler(TestingProviderRegistry registry, string host, string hostVersion) : IBridgeRequestHandler, IBridgeNotificationPublisher
{
    private readonly TestingProviderRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly string _host = host ?? throw new ArgumentNullException(nameof(host));
    private readonly string _hostVersion = hostVersion ?? throw new ArgumentNullException(nameof(hostVersion));
    private readonly TestingCancellationStateMachine _cancellation = new();
    private int _isBusy;

    public Action<string, JsonElement?>? NotificationSender { get; set; }

    public IReadOnlyCollection<string> SupportedMethods { get; } =
    [
        TestingProtocol.Hello,
        TestingProtocol.Run,
        TestingProtocol.Cancel,
    ];

    public TestingCancellationState CancellationState => _cancellation.State;

    public Task<BridgeMessage> HandleAsync(
        string requestId,
        string method,
        JsonElement? @params,
        CancellationToken ct = default)
    {
        if (string.Equals(method, TestingProtocol.Hello, StringComparison.OrdinalIgnoreCase))
        {
            if (TestingCancellationStateMachine.IsTerminal(_cancellation.State))
                _cancellation.Reset();

            return Task.FromResult(HandleHello(requestId, @params));
        }

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
        if (TryGetPoisonedRunError(requestId, out var poisoned))
            return poisoned;

        if (!TryReadRun(@params, out var request, out var error))
            return Invalid(requestId, error);

        if (!TryGetProvider(request!, requestId, out var provider, out var invalid))
            return invalid!;

        Interlocked.Exchange(ref _isBusy, 1);
        try
        {
            return RunProvider(request!, provider, requestId, cancellationToken);
        }
        finally
        {
            Interlocked.Exchange(ref _isBusy, 0);
        }
    }

    private bool TryGetPoisonedRunError(string requestId, out BridgeMessage response)
    {
        if (_cancellation.State != TestingCancellationState.Poisoned)
        {
            response = null!;
            return false;
        }

        response = BridgeMessage.Error(
            requestId,
            TestingErrorCodes.SessionPoisoned,
            "The testing session is poisoned.");
        return true;
    }

    private bool TryGetProvider(
        TestingRunRequest request,
        string requestId,
        out IHostTestFrameworkProvider provider,
        out BridgeMessage? invalid)
    {
        try
        {
            provider = _registry.GetRequired(request.FrameworkId);
            invalid = null;
            return true;
        }
        catch (KeyNotFoundException ex)
        {
            provider = null!;
            invalid = Invalid(requestId, ex.Message);
            return false;
        }
    }

    private BridgeMessage RunProvider(
        TestingRunRequest request,
        IHostTestFrameworkProvider provider,
        string requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            var sink = new HandlerEventSink(this);
            var response = provider.Run(request, sink, cancellationToken);
            ApplyCancellationState(response.CancellationState);

            return BridgeMessage.Response(
                requestId,
                JsonSerializer.SerializeToElement(response, TestingJsonContext.Default.TestingRunResponse));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return BridgeMessage.Error(
                requestId,
                IpcErrorCodes.InternalError,
                "Request cancelled because the client disconnected.");
        }
        catch (Exception ex)
        {
            PoisonSessionAfterProviderFailure();
            return BridgeMessage.Error(requestId, TestingErrorCodes.ProviderFailed, ex.ToString());
        }
    }

    private void ApplyCancellationState(TestingCancellationState runState)
    {
        if (runState == TestingCancellationState.Poisoned)
        {
            _cancellation.TryTransition(TestingCancellationState.Poisoned);
            return;
        }

        if (_cancellation.State == TestingCancellationState.Acknowledged)
            _cancellation.TryTransition(TestingCancellationState.Completed);
    }

    private void PoisonSessionAfterProviderFailure()
    {
        if (_cancellation.State == TestingCancellationState.None)
            _cancellation.TryTransition(TestingCancellationState.Requested);

        _cancellation.TryTransition(TestingCancellationState.Poisoned);
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

    private sealed class HandlerEventSink(DotnetTestRequestHandler owner) : ITestingEventSink
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
