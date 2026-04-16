using System.Text.Json;
using ModelContextProtocol.Protocol;
using RevitDevTool.Controllers;
using RevitDevTool.Core;
using RevitDevTool.McpParser.Models;
using RevitDevTool.ExternalExecution.Mcp.Dispatchers;
using RevitDevTool.ExternalExecution.Connections;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.ExternalExecution.Mcp.Handlers;

public sealed class RegistryRequestHandler(
    ToolRegistryStore toolStore,
    ConnectionState state,
    ToolExecutionDispatcher dispatcher,
    PromptExecutionDispatcher promptDispatcher,
    ResourceExecutionDispatcher resourceDispatcher)
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    public Task<BridgeMessage> HandleToolsListAsync(string id)
    {
        toolStore.EnsureLoaded();
        var tools = toolStore.Tools.ToList();
        var json = JsonSerializer.SerializeToElement(tools);
        return Task.FromResult(BridgeMessage.Response(id, json));
    }

    public async Task<BridgeMessage> HandleToolsCallAsync(string id, JsonElement? @params)
    {
        string? toolName = null;
        if (@params?.TryGetProperty("name", out var nameElement) == true)
            toolName = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(toolName))
            return BridgeMessage.Error(id, "Tool name is required.");

        var resolvedToolName = toolName!;

        toolStore.EnsureLoaded();
        if (!toolStore.TryGetTool(null, resolvedToolName, out var tool) || tool is null)
            return BridgeMessage.Error(id, $"Tool '{resolvedToolName}' is not registered.");

        var payloadJson = "{}";
        if (@params?.TryGetProperty("arguments", out var argsElement) == true)
            payloadJson = argsElement.GetRawText();

        using var scope = state.BeginExecution(resolvedToolName);

        var handler = await ExternalEventController
            .AsyncGenericEventHandler<McpToolExecutionResult>()
            .ConfigureAwait(false);

        scope.MarkRunning();
        McpToolExecutionResult? result;
        var toolTimeoutMessage = $"Tool '{resolvedToolName}' exceeded timeout ({CallTimeout.TotalSeconds:F0}s).";
        try
        {
            result = await handler
                .RaiseAsync(() => dispatcher.DispatchAsync(tool, payloadJson), CallTimeout)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            var failed = McpToolExecutionResult.Failed(
                ExecutionErrorCodes.ToolInvokeFailed,
                toolTimeoutMessage);
            scope.Complete(failed);
            return BridgeMessage.Error(id, failed.Error?.Message ?? failed.Detail);
        }

        if (result is null)
        {
            var failed = McpToolExecutionResult.Failed(
                ExecutionErrorCodes.ToolInvokeFailed,
                $"Tool '{resolvedToolName}' returned no result.");
            scope.Complete(failed);
            return BridgeMessage.Error(id, failed.Error?.Message ?? failed.Detail);
        }

        scope.Complete(result);
        if (result is { State: ExecutionState.Completed })
        {
            state.RecordCall(tool.Id, tool.ProtocolTool.Name);
            var json = JsonSerializer.SerializeToElement(result.Result);
            return BridgeMessage.Response(id, json);
        }

        return BridgeMessage.Error(id, result.Error?.Message ?? result.Detail);
    }

    public Task<BridgeMessage> HandlePromptsListAsync(string id)
    {
        toolStore.EnsureLoaded();
        var prompts = toolStore.Prompts.ToList();
        var json = JsonSerializer.SerializeToElement(prompts);
        return Task.FromResult(BridgeMessage.Response(id, json));
    }

    public async Task<BridgeMessage> HandlePromptsGetAsync(string id, JsonElement? @params)
    {
        string? promptName = null;
        if (@params?.TryGetProperty("name", out var nameElement) == true)
            promptName = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(promptName))
            return BridgeMessage.Error(id, "Prompt name is required.");

        toolStore.EnsureLoaded();
        if (!toolStore.TryGetPrompt(null, promptName, out var prompt) || prompt is null)
            return BridgeMessage.Error(id, $"Prompt '{promptName}' is not registered.");

        Dictionary<string, JsonElement>? arguments = null;
        if (@params?.TryGetProperty("arguments", out var argsElement) == true)
            arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsElement.GetRawText());

        var handler = await ExternalEventController
            .AsyncGenericEventHandler<GetPromptResult>()
            .ConfigureAwait(false);

        GetPromptResult? result;
        try
        {
            result = await handler
                .RaiseAsync(() => promptDispatcher.GetPromptAsync(prompt, arguments), CallTimeout)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return BridgeMessage.Error(id, $"Prompt '{promptName}' exceeded timeout ({CallTimeout.TotalSeconds:F0}s).");
        }

        if (result is null)
            return BridgeMessage.Error(id, $"Prompt '{promptName}' returned no result.");

        var json = JsonSerializer.SerializeToElement(result);
        return BridgeMessage.Response(id, json);
    }

    public Task<BridgeMessage> HandleResourcesListAsync(string id)
    {
        toolStore.EnsureLoaded();
        var resources = toolStore.DirectResources.ToList();
        var json = JsonSerializer.SerializeToElement(resources);
        return Task.FromResult(BridgeMessage.Response(id, json));
    }

    public Task<BridgeMessage> HandleResourceTemplatesListAsync(string id)
    {
        toolStore.EnsureLoaded();
        var templates = toolStore.ResourceTemplates.ToList();
        var json = JsonSerializer.SerializeToElement(templates);
        return Task.FromResult(BridgeMessage.Response(id, json));
    }

    public async Task<BridgeMessage> HandleResourcesReadAsync(string id, JsonElement? @params)
    {
        string? uri = null;
        if (@params?.TryGetProperty("uri", out var uriElement) == true)
            uri = uriElement.GetString();
        if (string.IsNullOrWhiteSpace(uri))
            return BridgeMessage.Error(id, "Resource URI is required.");

        var resolvedUri = uri!;

        toolStore.EnsureLoaded();
        if (!toolStore.TryResolveResourceByUri(resolvedUri, out var resource) || resource is null)
            return BridgeMessage.Error(id, $"Resource '{resolvedUri}' is not registered.");

        var handler = await ExternalEventController
            .AsyncGenericEventHandler<ReadResourceResult>()
            .ConfigureAwait(false);

        ReadResourceResult? result;
        try
        {
            result = await handler
                .RaiseAsync(() => resourceDispatcher.ReadResourceAsync(resource, resolvedUri), CallTimeout)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return BridgeMessage.Error(id, $"Resource '{resolvedUri}' exceeded timeout ({CallTimeout.TotalSeconds:F0}s).");
        }

        if (result is null)
            return BridgeMessage.Error(id, $"Resource '{resolvedUri}' returned no result.");

        var json = JsonSerializer.SerializeToElement(result);
        return BridgeMessage.Response(id, json);
    }

    public void ClearCaches()
    {
        dispatcher.ClearCache();
        promptDispatcher.ClearCache();
        resourceDispatcher.ClearCache();
    }
}
