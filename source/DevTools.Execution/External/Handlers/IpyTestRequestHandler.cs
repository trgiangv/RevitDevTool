using DevTools.Telemetry;
using System.Text.Json;

namespace DevTools.Execution.External.Handlers;

/// <summary>
/// IronPython unittest over <c>ipytests/run</c>. No PEP 723 / pixi prepare —
/// that path is CPython <see cref="PytestRequestHandler"/> only.
/// </summary>
public sealed class IpyTestRequestHandler(
    IpyTestExecutionService executionService,
    ITelemetry? telemetry = null) : IBridgeRequestHandler
{
    private readonly ITelemetry _telemetry = telemetry ?? new NoOpTelemetry();

    public IReadOnlyCollection<string> SupportedMethods { get; } = [PytestBridgeMethods.IpyTestsRun];

    public Task<BridgeMessage> HandleAsync(
        string requestId,
        string method,
        JsonElement? @params,
        CancellationToken ct = default)
    {
        return string.Equals(method, PytestBridgeMethods.IpyTestsRun, StringComparison.OrdinalIgnoreCase)
            ? HandleRunAsync(requestId, @params, ct)
            : Task.FromResult(BridgeMessage.Error(requestId, IpcErrorCodes.MethodNotFound, $"Unknown method: {method}"));
    }

    private async Task<BridgeMessage> HandleRunAsync(string id, JsonElement? @params, CancellationToken ct)
    {
        using var operation = _telemetry.StartOperation(TelemetryKeys.Operation.Test, "ipytest");
        if (!IpyTestExecutionService.TryParseRunRequest(@params, out var request, out var error))
        {
            operation.Fail();
            var invalid = IpyTestExecutionService.Error("prepare", error ?? "Invalid ipytests/run request.");
            return BridgeMessage.Response(id, JsonSerializer.SerializeToElement(invalid));
        }

        PytestRunResponse result;
        try
        {
            ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
            result = await executionService.RunAsync(request!, ct).ConfigureAwait(false);
            operation.Complete();
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            result = IpyTestExecutionService.Error("run", "Failed to execute IronPython unittest session.", ex.ToString());
        }

        return BridgeMessage.Response(id, JsonSerializer.SerializeToElement(result));
    }
}
