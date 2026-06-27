using System.Text.Json;
using DevTools.Mcp.Dispatch;
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
            BridgeMessage.ErrorResponse(requestId, McpExecutionErrorCodes.ToolUnknownSourceKind, $"Unknown method: {method}"));
    }

    public Task<BridgeMessage> HandleToolsListAsync(string id)
    {
        catalogStore.EnsureLoaded();
        var tools = catalogStore.Tools.ToList();
        var json = JsonSerializer.SerializeToElement(tools);
        return Task.FromResult(BridgeMessage.Response(id, json));
    }

    public async Task<BridgeMessage> HandleToolsCallAsync(string id, JsonElement? @params)
    {
        string? toolName = null;
        if (@params?.TryGetProperty(McpPropertyNames.Name, out var nameElement) == true)
            toolName = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(toolName))
            return BridgeMessage.ErrorResponse(id, McpExecutionErrorCodes.ToolInvokeFailed, "Tool name is required.");

        var resolvedToolName = toolName!;

        catalogStore.EnsureLoaded();
        if (!catalogStore.TryGetTool(null, resolvedToolName, out var tool) || tool is null)
            return BridgeMessage.ErrorResponse(id, McpExecutionErrorCodes.ToolNotFound, $"Tool '{resolvedToolName}' is not registered.");

        var payloadJson = "{}";
        if (@params?.TryGetProperty(McpPropertyNames.Arguments, out var argsElement) == true)
            payloadJson = argsElement.GetRawText();

        using var scope = executionTracker.BeginExecution(resolvedToolName);

        executionTracker.MarkRunning(scope);
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
            return BridgeMessage.ErrorResponse(id, failed.Error?.Code ?? McpExecutionErrorCodes.ToolInvokeFailed,
                failed.Error?.Message ?? failed.Detail);
        }

        executionTracker.Complete(scope, result);

        if (result is not { State: ExecutionState.Completed })
            return BridgeMessage.ErrorResponse(id,
                result.Error?.Code ?? McpExecutionErrorCodes.ToolInvokeFailed,
                result.Error?.Message ?? result.Detail);

        executionTracker.RecordCall(tool.Id, tool.ProtocolTool.Name);
        var json = JsonSerializer.SerializeToElement(result.Result);
        return BridgeMessage.Response(id, json);
    }

    public Task<BridgeMessage> HandlePromptsListAsync(string id)
    {
        catalogStore.EnsureLoaded();
        var prompts = catalogStore.Prompts.ToList();
        var json = JsonSerializer.SerializeToElement(prompts);
        return Task.FromResult(BridgeMessage.Response(id, json));
    }

    public async Task<BridgeMessage> HandlePromptsGetAsync(string id, JsonElement? @params)
    {
        string? promptName = null;
        if (@params?.TryGetProperty(McpPropertyNames.Name, out var nameElement) == true)
            promptName = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(promptName))
            return BridgeMessage.ErrorResponse(id, McpExecutionErrorCodes.PromptInvokeFailed, "Prompt name is required.");

        catalogStore.EnsureLoaded();
        if (!catalogStore.TryGetPrompt(null, promptName, out var prompt) || prompt is null)
            return BridgeMessage.ErrorResponse(id, McpExecutionErrorCodes.PromptNotFound, $"Prompt '{promptName}' is not registered.");

        Dictionary<string, JsonElement>? arguments = null;
        if (@params?.TryGetProperty(McpPropertyNames.Arguments, out var argsElement) == true)
            arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsElement.GetRawText());

        GetPromptResult result;
        try
        {
            using var cts = new CancellationTokenSource(CallTimeout);
            result = await hostContext
                .ExecuteAsync(() => dispatcher.GetPrompt(prompt, arguments, cts.Token), cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return BridgeMessage.ErrorResponse(id, McpExecutionErrorCodes.PromptInvokeFailed,
                $"Prompt '{promptName}' exceeded timeout ({CallTimeout.TotalSeconds:F0}s).");
        }

        var json = JsonSerializer.SerializeToElement(result);
        return BridgeMessage.Response(id, json);
    }

    public Task<BridgeMessage> HandleResourcesListAsync(string id)
    {
        catalogStore.EnsureLoaded();
        var resources = catalogStore.DirectResources.ToList();
        var json = JsonSerializer.SerializeToElement(resources);
        return Task.FromResult(BridgeMessage.Response(id, json));
    }

    public Task<BridgeMessage> HandleResourceTemplatesListAsync(string id)
    {
        catalogStore.EnsureLoaded();
        var templates = catalogStore.ResourceTemplates.ToList();
        var json = JsonSerializer.SerializeToElement(templates);
        return Task.FromResult(BridgeMessage.Response(id, json));
    }

    public async Task<BridgeMessage> HandleResourcesReadAsync(string id, JsonElement? @params)
    {
        string? uri = null;
        if (@params?.TryGetProperty(McpPropertyNames.Uri, out var uriElement) == true)
            uri = uriElement.GetString();
        if (string.IsNullOrWhiteSpace(uri))
            return BridgeMessage.ErrorResponse(id, McpExecutionErrorCodes.ResourceReadFailed, "Resource URI is required.");

        var resolvedUri = uri!;

        catalogStore.EnsureLoaded();
        if (!catalogStore.TryResolveResourceByUri(resolvedUri, out var resource) || resource is null)
            return BridgeMessage.ErrorResponse(id, McpExecutionErrorCodes.ResourceNotFound, $"Resource '{resolvedUri}' is not registered.");

        ReadResourceResult result;
        try
        {
            using var cts = new CancellationTokenSource(CallTimeout);
            result = await hostContext
                .ExecuteAsync(() => dispatcher.ReadResource(resource, resolvedUri, cts.Token), cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return BridgeMessage.ErrorResponse(id, McpExecutionErrorCodes.ResourceReadFailed,
                $"Resource '{resolvedUri}' exceeded timeout ({CallTimeout.TotalSeconds:F0}s).");
        }

        var json = JsonSerializer.SerializeToElement(result);
        return BridgeMessage.Response(id, json);
    }

    public void ClearCaches() => dispatcher.ClearCaches();
}
