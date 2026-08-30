using System.Text.Json;
namespace DevTools.Execution.External.Handlers;

/// <summary>
/// CPython pytest over <c>tests/run</c>. PEP 723 prepare runs here via
/// <see cref="PytestDependencyService"/>; IronPython uses
/// <see cref="IpyTestRequestHandler"/> and never installs those packages.
/// </summary>
public sealed class PytestRequestHandler(
    IHostContextExecutor hostContext,
    PytestDependencyService dependencyService,
    PytestExecutionService executionService) : IBridgeRequestHandler, IBridgeNotificationPublisher
{
    public Action<string, JsonElement?>? NotificationSender { get; set; }

    public IReadOnlyCollection<string> SupportedMethods { get; } =
    [
        PytestBridgeMethods.TestsRun,
    ];

    public Task<BridgeMessage> HandleAsync(
        string requestId,
        string method,
        JsonElement? @params,
        CancellationToken ct = default)
    {
        return string.Equals(method, PytestBridgeMethods.TestsRun, StringComparison.OrdinalIgnoreCase) 
            ? HandleRunAsync(requestId, @params) 
            : Task.FromResult(BridgeMessage.Error(requestId, IpcErrorCodes.MethodNotFound, $"Unknown method: {method}"));

    }

    private async Task<BridgeMessage> HandleRunAsync(string id, JsonElement? @params)
    {
        if (!PytestExecutionService.TryParseRunRequest(@params, out var request, out var error))
        {
            var invalidRequest = PytestExecutionService.Error("prepare", error ?? "Invalid pytest run request.");
            var invalidRequestJson = JsonSerializer.SerializeToElement(invalidRequest);
            return BridgeMessage.Response(id, invalidRequestJson);
        }

        try
        {
            await dependencyService.PrepareRunAsync(request!).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var prepareFailure = PytestExecutionService.Error("prepare", "Failed to prepare pytest session.", ex.ToString());
            var prepareJson = JsonSerializer.SerializeToElement(prepareFailure);
            return BridgeMessage.Response(id, prepareJson);
        }

        PytestRunResponse result;
        try
        {
            ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
            result = await hostContext
                .ExecuteAsync(() => executionService.Run(request!, CreateProgressCallback()))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            result = PytestExecutionService.Error("run", "Failed to execute pytest session.", ex.ToString());
        }

        var json = JsonSerializer.SerializeToElement(result);
        return BridgeMessage.Response(id, json);
    }

    private Action<string>? CreateProgressCallback()
    {
        var sender = NotificationSender;
        if (sender is null)
            return null;

        return resultJson => sender(
            PytestBridgeMethods.NotifyTestProgress,
            JsonSerializer.Deserialize<JsonElement>(resultJson));
    }
}
