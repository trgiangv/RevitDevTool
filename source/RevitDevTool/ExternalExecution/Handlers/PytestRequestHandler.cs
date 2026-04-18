using System.Text.Json;
using RevitDevTool.Core;
using RevitDevTool.ExternalExecution.Testing;
using DevTool.McpParser.Models;

namespace RevitDevTool.ExternalExecution.Handlers;

public sealed class PytestRequestHandler(
    PytestDependencyService dependencyService,
    PytestExecutionService executionService)
{
    public async Task<BridgeMessage> HandleDiscoverAsync(string id, JsonElement? @params)
    {
        if (!PytestExecutionService.TryParseDiscoverRequest(@params, out var request, out var error))
            return BridgeMessage.Error(id, error ?? "Invalid pytest discover request.");

        await dependencyService.PrepareDiscoverAsync(request!).ConfigureAwait(false);

        var result = await RevitContextExecutor
            .RaiseAsync(() => executionService.Discover(request!))
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
            result = await RevitContextExecutor
                .RaiseAsync(() => executionService.Run(request!))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            result = PytestExecutionService.Error("run", "Failed to execute pytest session.", ex.ToString());
        }

        var json = JsonSerializer.SerializeToElement(result);
        return BridgeMessage.Response(id, json);
    }
}
