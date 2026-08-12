using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Logging;
using DevTools.NUnit.Core.Compatibility;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Transport;

namespace DevTools.NUnit.Host;

public static class NUnitErrorCodes
{
    public const string AssemblyLoadFailed = "nunit/assembly_load_failed";
    public const string InvalidRequest = "nunit/invalid_request";
}

/// <summary>
/// Bridge handler for the <c>nunit/*</c> host-test protocol.
/// Discover and run requests are marshaled through <see cref="IHostContextExecutor"/>.
/// </summary>
/// <remarks>
/// <para>
/// On Revit, <see cref="ExecutionGuardContext.Mode"/> is set to
/// <see cref="ExecutionGuardMode.Suppress"/> during <c>nunit/run</c>, matching pytest behavior.
/// AutoCAD does not yet provide an equivalent execution guard; the mode is still set but has no effect there.
/// </para>
/// </remarks>
public sealed class NUnitRequestHandler(
    IHostContextExecutor hostContext,
    INUnitHost nunitHost,
    IHostAppInfo hostInfo) : IBridgeRequestHandler, IBridgeNotificationPublisher
{
    private int _isBusy;

    public Action<string, JsonElement?>? NotificationSender { get; set; }

    public IReadOnlyCollection<string> SupportedMethods { get; } =
    [
        NUnitProtocol.Hello,
        NUnitProtocol.Discover,
        NUnitProtocol.Run,
        NUnitProtocol.Cancel,
    ];

    public Task<BridgeMessage> HandleAsync(
        string requestId,
        string method,
        JsonElement? @params,
        CancellationToken ct = default)
    {
        if (string.Equals(method, NUnitProtocol.Hello, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(HandleHello(requestId, @params));

        if (string.Equals(method, NUnitProtocol.Discover, StringComparison.OrdinalIgnoreCase))
            return HandleDiscoverAsync(requestId, @params, ct);

        if (string.Equals(method, NUnitProtocol.Run, StringComparison.OrdinalIgnoreCase))
            return HandleRunAsync(requestId, @params, ct);

        if (string.Equals(method, NUnitProtocol.Cancel, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(HandleCancel(requestId, @params));

        return Task.FromResult(BridgeMessage.Error(
            requestId,
            IpcErrorCodes.MethodNotFound,
            $"Unknown method: {method}"));
    }

    private BridgeMessage HandleHello(string requestId, JsonElement? @params)
    {
        if (!TryDeserializeHello(@params, out var request, out var error))
            return CreateInvalidRequest(requestId, error);

        var compatibilityError = ProtocolCompatibility.Validate(request!.ProtocolVersion);
        if (compatibilityError is not null)
            return NUnitProtocolBridge.CreateIncompatibleResponse(requestId, request.ProtocolVersion);

        var response = new NUnitHelloResponse(
            ProtocolVersion: NUnitProtocol.CurrentVersion,
            Host: hostInfo.Host.ToString(),
            HostVersion: hostInfo.VersionNumber,
            ProcessId: Environment.ProcessId,
            IsBusy: Volatile.Read(ref _isBusy) != 0);

        return BridgeMessage.Response(
            requestId,
            JsonSerializer.SerializeToElement(response, NUnitJsonContext.Default.NUnitHelloResponse));
    }

    private async Task<BridgeMessage> HandleDiscoverAsync(
        string requestId,
        JsonElement? @params,
        CancellationToken ct)
    {
        if (!TryDeserializeDiscover(@params, out var request, out var error))
            return CreateInvalidRequest(requestId, error);

        try
        {
            var response = await hostContext
                .ExecuteAsync(() => nunitHost.Discover(request!), ct)
                .ConfigureAwait(false);

            return BridgeMessage.Response(
                requestId,
                JsonSerializer.SerializeToElement(response, NUnitJsonContext.Default.NUnitDiscoverResponse));
        }
        catch (NUnitAssemblyLoadException ex)
        {
            return CreateAssemblyLoadError(requestId, ex.Result);
        }
        catch (Exception ex)
        {
            return BridgeMessage.Error(requestId, IpcErrorCodes.InternalError, ex.Message);
        }
    }

    private async Task<BridgeMessage> HandleRunAsync(
        string requestId,
        JsonElement? @params,
        CancellationToken ct)
    {
        if (!TryDeserializeRun(@params, out var request, out var error))
            return CreateInvalidRequest(requestId, error);

        Interlocked.Exchange(ref _isBusy, 1);
        try
        {
            NUnitRunResponse response;
            try
            {
                ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
                response = await hostContext
                    .ExecuteAsync(() => nunitHost.Run(request!, PublishProgress, ct), ct)
                    .ConfigureAwait(false);
            }
            catch (NUnitAssemblyLoadException ex)
            {
                return CreateAssemblyLoadError(requestId, ex.Result);
            }
            catch (Exception ex)
            {
                return BridgeMessage.Error(requestId, IpcErrorCodes.InternalError, ex.Message);
            }

            return BridgeMessage.Response(
                requestId,
                JsonSerializer.SerializeToElement(response, NUnitJsonContext.Default.NUnitRunResponse));
        }
        finally
        {
            Interlocked.Exchange(ref _isBusy, 0);
        }
    }

    private BridgeMessage HandleCancel(string requestId, JsonElement? @params)
    {
        if (!TryDeserializeCancel(@params, out var request, out var error))
            return CreateInvalidRequest(requestId, error);

        nunitHost.Cancel(request!.RunId);
        return BridgeMessage.Response(requestId, null);
    }

    private void PublishProgress(NUnitProgressEvent progressEvent)
    {
        var sender = NotificationSender;
        if (sender is null)
            return;

        sender(
            NUnitProtocol.Progress,
            JsonSerializer.SerializeToElement(progressEvent, NUnitJsonContext.Default.NUnitProgressEvent));
    }

    private static bool TryDeserializeHello(JsonElement? @params, out NUnitHelloRequest? value, out string error)
    {
        value = null;
        error = string.Empty;
        if (@params is null)
        {
            error = "Request params are required.";
            return false;
        }

        try
        {
            value = @params.Value.Deserialize(NUnitJsonContext.Default.NUnitHelloRequest);
            return value is not null;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryDeserializeDiscover(JsonElement? @params, out NUnitDiscoverRequest? value, out string error)
    {
        value = null;
        error = string.Empty;
        if (@params is null)
        {
            error = "Request params are required.";
            return false;
        }

        try
        {
            value = @params.Value.Deserialize(NUnitJsonContext.Default.NUnitDiscoverRequest);
            return value is not null;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryDeserializeRun(JsonElement? @params, out NUnitRunRequest? value, out string error)
    {
        value = null;
        error = string.Empty;
        if (@params is null)
        {
            error = "Request params are required.";
            return false;
        }

        try
        {
            value = @params.Value.Deserialize(NUnitJsonContext.Default.NUnitRunRequest);
            return value is not null;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryDeserializeCancel(JsonElement? @params, out NUnitCancelRequest? value, out string error)
    {
        value = null;
        error = string.Empty;
        if (@params is null)
        {
            error = "Request params are required.";
            return false;
        }

        try
        {
            value = @params.Value.Deserialize(NUnitJsonContext.Default.NUnitCancelRequest);
            return value is not null;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static BridgeMessage CreateInvalidRequest(string requestId, string error) =>
        BridgeMessage.Error(requestId, NUnitErrorCodes.InvalidRequest, error);

    private static BridgeMessage CreateAssemblyLoadError(string requestId, NUnitAssemblyPreflightResult result) =>
        BridgeMessage.Error(
            requestId,
            NUnitErrorCodes.AssemblyLoadFailed,
            result.Message ?? "Failed to load test assembly.",
            JsonSerializer.SerializeToElement(new
            {
                assembly_path = result.AssemblyPath,
                details = result.Details,
            }));
}
