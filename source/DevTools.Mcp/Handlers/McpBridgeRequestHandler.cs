using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Handlers;

/// <summary>
/// In-host bridge request handler for all MCP primitive operations.
/// Routes <c>tools/list</c>, <c>tools/call</c>, <c>prompts/list</c>, <c>prompts/get</c>,
/// <c>resources/list</c>, <c>resources/templates/list</c>, and <c>resources/read</c>
/// to the catalog store and primitive dispatcher.
/// </summary>
public sealed class McpBridgeRequestHandler(
    McpCatalogStore catalogStore,
    IMcpExecutionTracker executionTracker,
    IHostContextExecutor hostContext,
    IMcpPrimitiveDispatcher dispatcher) : IBridgeRequestHandler
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(120);

    public IReadOnlyCollection<string> SupportedMethods { get; } =
    [
        McpBridgeMethods.ToolsList,
        McpBridgeMethods.ToolsCall,
        McpBridgeMethods.PromptsList,
        McpBridgeMethods.PromptsGet,
        McpBridgeMethods.ResourcesList,
        McpBridgeMethods.ResourceTemplatesList,
        McpBridgeMethods.ResourcesRead,
    ];

    public Task<BridgeMessage> HandleAsync(
        string requestId,
        string method,
        JsonElement? @params,
        CancellationToken ct = default)
    {
        if (string.Equals(method, McpBridgeMethods.ToolsList, StringComparison.OrdinalIgnoreCase))
            return HandleToolsListAsync(requestId);
        if (string.Equals(method, McpBridgeMethods.ToolsCall, StringComparison.OrdinalIgnoreCase))
            return HandleToolsCallAsync(requestId, @params);
        if (string.Equals(method, McpBridgeMethods.PromptsList, StringComparison.OrdinalIgnoreCase))
            return HandlePromptsListAsync(requestId);
        if (string.Equals(method, McpBridgeMethods.PromptsGet, StringComparison.OrdinalIgnoreCase))
            return HandlePromptsGetAsync(requestId, @params);
        if (string.Equals(method, McpBridgeMethods.ResourcesList, StringComparison.OrdinalIgnoreCase))
            return HandleResourcesListAsync(requestId);
        if (string.Equals(method, McpBridgeMethods.ResourceTemplatesList, StringComparison.OrdinalIgnoreCase))
            return HandleResourceTemplatesListAsync(requestId);
        if (string.Equals(method, McpBridgeMethods.ResourcesRead, StringComparison.OrdinalIgnoreCase))
            return HandleResourcesReadAsync(requestId, @params);

        return Task.FromResult(
            BridgeMessage.Error(requestId, McpExecutionErrorCodes.ToolUnknownSourceKind, $"Unknown method: {method}"));
    }

    private Task<BridgeMessage> HandleToolsListAsync(string id)
    {
        catalogStore.EnsureLoaded();
        var tools = catalogStore.Tools.ToList();
        var json = JsonSerializer.SerializeToElement(tools);
        return Task.FromResult(BridgeMessage.Response(id, json));
    }

    private async Task<BridgeMessage> HandleToolsCallAsync(string id, JsonElement? @params)
    {
        var callParams = @params is { } p ? p.Deserialize<McpToolsCallParams>() : null;
        if (callParams is null || string.IsNullOrWhiteSpace(callParams.Name))
            return BridgeMessage.Error(id, McpExecutionErrorCodes.ToolInvokeFailed, "Tool name is required.");

        var resolvedToolName = callParams.Name;

        catalogStore.EnsureLoaded();
        if (!catalogStore.TryGetTool(null, resolvedToolName, out var tool) || tool is null)
            return BridgeMessage.Error(id, McpExecutionErrorCodes.ToolNotFound, $"Tool '{resolvedToolName}' is not registered.");

        var payloadJson = callParams.Arguments is { } args ? JsonSerializer.Serialize(args) : "{}";

        using var scope = executionTracker.BeginExecution(resolvedToolName);

        executionTracker.MarkRunning(scope);
        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        McpToolExecutionResult result;
        try
        {
            using var cts = new CancellationTokenSource(CallTimeout);
            result = await dispatcher.DispatchToolAsync(tool, payloadJson, hostContext, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var failed = McpToolExecutionResult.Failed(
                McpExecutionErrorCodes.ToolInvokeFailed,
                $"Tool '{resolvedToolName}' exceeded timeout ({CallTimeout.TotalSeconds:F0}s).");
            executionTracker.Complete(scope, failed);
            return BridgeMessage.Error(id, failed.Error?.Code ?? McpExecutionErrorCodes.ToolInvokeFailed,
                failed.Error?.Message ?? failed.Detail);
        }

        executionTracker.Complete(scope, result);

        if (result is not { State: ExecutionState.Completed })
            return BridgeMessage.Error(id,
                result.Error?.Code ?? McpExecutionErrorCodes.ToolInvokeFailed,
                result.Error?.Message ?? result.Detail);

        executionTracker.RecordCall(tool.Id, tool.ProtocolTool.Name);
        var json = JsonSerializer.SerializeToElement(result.Result);
        return BridgeMessage.Response(id, json);
    }

    private Task<BridgeMessage> HandlePromptsListAsync(string id)
    {
        catalogStore.EnsureLoaded();
        var prompts = catalogStore.Prompts.ToList();
        var json = JsonSerializer.SerializeToElement(prompts);
        return Task.FromResult(BridgeMessage.Response(id, json));
    }

    private async Task<BridgeMessage> HandlePromptsGetAsync(string id, JsonElement? @params)
    {
        var getParams = @params is { } p ? p.Deserialize<McpPromptsGetParams>() : null;
        if (getParams is null || string.IsNullOrWhiteSpace(getParams.Name))
            return BridgeMessage.Error(id, McpExecutionErrorCodes.PromptInvokeFailed, "Prompt name is required.");

        var promptName = getParams.Name;

        catalogStore.EnsureLoaded();
        if (!catalogStore.TryGetPrompt(null, promptName, out var prompt) || prompt is null)
            return BridgeMessage.Error(id, McpExecutionErrorCodes.PromptNotFound, $"Prompt '{promptName}' is not registered.");

        var arguments = getParams.Arguments;

        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        GetPromptResult result;
        try
        {
            using var cts = new CancellationTokenSource(CallTimeout);
            var token = cts.Token;
            result = await hostContext
                .ExecuteAsync(() => dispatcher.GetPrompt(prompt, arguments, token), token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return BridgeMessage.Error(id, McpExecutionErrorCodes.PromptInvokeFailed,
                $"Prompt '{promptName}' exceeded timeout ({CallTimeout.TotalSeconds:F0}s).");
        }

        var json = JsonSerializer.SerializeToElement(result);
        return BridgeMessage.Response(id, json);
    }

    private Task<BridgeMessage> HandleResourcesListAsync(string id)
    {
        catalogStore.EnsureLoaded();
        var resources = catalogStore.DirectResources.ToList();
        var json = JsonSerializer.SerializeToElement(resources);
        return Task.FromResult(BridgeMessage.Response(id, json));
    }

    private Task<BridgeMessage> HandleResourceTemplatesListAsync(string id)
    {
        catalogStore.EnsureLoaded();
        var templates = catalogStore.ResourceTemplates.ToList();
        var json = JsonSerializer.SerializeToElement(templates);
        return Task.FromResult(BridgeMessage.Response(id, json));
    }

    private async Task<BridgeMessage> HandleResourcesReadAsync(string id, JsonElement? @params)
    {
        var readParams = @params is { } p ? p.Deserialize<McpResourcesReadParams>() : null;
        if (readParams is null || string.IsNullOrWhiteSpace(readParams.Uri))
            return BridgeMessage.Error(id, McpExecutionErrorCodes.ResourceReadFailed, "Resource URI is required.");

        var resolvedUri = readParams.Uri;

        catalogStore.EnsureLoaded();
        if (!catalogStore.TryResolveResourceByUri(resolvedUri, out var resource) || resource is null)
            return BridgeMessage.Error(id, McpExecutionErrorCodes.ResourceNotFound, $"Resource '{resolvedUri}' is not registered.");

        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        ReadResourceResult result;
        try
        {
            using var cts = new CancellationTokenSource(CallTimeout);
            var token = cts.Token;
            result = await hostContext
                .ExecuteAsync(() => dispatcher.ReadResource(resource, resolvedUri, token), token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return BridgeMessage.Error(id, McpExecutionErrorCodes.ResourceReadFailed,
                $"Resource '{resolvedUri}' exceeded timeout ({CallTimeout.TotalSeconds:F0}s).");
        }

        var json = JsonSerializer.SerializeToElement(result);
        return BridgeMessage.Response(id, json);
    }
}
