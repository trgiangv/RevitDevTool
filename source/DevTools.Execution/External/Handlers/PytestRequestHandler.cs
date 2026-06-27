using System.Text.Json;
using DevTools.Execution.External.Testing;
using DevTools.Execution.Interfaces;
namespace DevTools.Execution.External.Handlers;

public sealed class PytestRequestHandler(
    IHostContextExecutor hostContext,
    PytestDependencyService dependencyService,
    PytestExecutionService executionService)
{
    /// <summary>
    /// Injected by the pipe server to broadcast notifications to connected clients.
    /// </summary>
    public Action<string, object?>? NotifySender { get; set; }

    public async Task<BridgeMessage> HandleDiscoverAsync(string id, JsonElement? @params)
    {
        if (!PytestExecutionService.TryParseDiscoverRequest(@params, out var request, out var error))
            return BridgeMessage.Error(id, error ?? "Invalid pytest discover request.");

        await dependencyService.PrepareDiscoverAsync(request!).ConfigureAwait(false);

        var result = await hostContext
            .ExecuteAsync(() => executionService.Discover(request!))
            .ConfigureAwait(false);

        var json = JsonSerializer.SerializeToElement(result);
        return BridgeMessage.Response(id, json);
    }

    public async Task<BridgeMessage> HandleRunAsync(string id, JsonElement? @params)
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
        var sender = NotifySender;
        if (sender is null)
            return null;

        return resultJson => sender(PytestBridgeMethods.NotifyTestProgress, resultJson);
    }
}
