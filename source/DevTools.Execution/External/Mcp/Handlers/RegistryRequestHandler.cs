using System.Text.Json;
using DevTools.Execution.External.Connections;
using DevTools.Execution.External.Mcp.Dispatchers;
using DevTools.Execution.Interfaces;
using DevTools.McpParser.Dotnet;
using DevTools.McpParser.Models;
using ModelContextProtocol.Protocol;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Execution.External.Mcp.Handlers;

public sealed class RegistryRequestHandler(
    ToolRegistryStore toolStore,
    ConnectionState state,
    IHostContextExecutor hostContext,
    ToolExecutionDispatcher toolDispatcher,
    PromptExecutionDispatcher promptDispatcher,
    ResourceExecutionDispatcher resourceDispatcher,
    McpToolsetContextManager toolsetContextManager)
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);
    private const string Name = "name";
    private const string Args = "arguments";
    private const string Uri = "uri";

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
        if (@params?.TryGetProperty(Name, out var nameElement) == true)
            toolName = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(toolName))
            return BridgeMessage.Error(id, "Tool name is required.");

        var resolvedToolName = toolName!;

        toolStore.EnsureLoaded();
        if (!toolStore.TryGetTool(null, resolvedToolName, out var tool) || tool is null)
            return BridgeMessage.Error(id, $"Tool '{resolvedToolName}' is not registered.");

        var payloadJson = "{}";
        if (@params?.TryGetProperty(Args, out var argsElement) == true)
            payloadJson = argsElement.GetRawText();

        using var scope = state.BeginExecution(resolvedToolName);

        scope.MarkRunning();
        McpToolExecutionResult result;
        try
        {
            using var cts = new CancellationTokenSource(CallTimeout);
            result = await toolDispatcher.DispatchAsync(tool, payloadJson, hostContext, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var failed = McpToolExecutionResult.Failed(
                ExecutionErrorCodes.ToolInvokeFailed,
                $"Tool '{resolvedToolName}' exceeded timeout ({CallTimeout.TotalSeconds:F0}s).");
            scope.Complete(failed);
            return BridgeMessage.Error(id, failed.Error?.Message ?? failed.Detail);
        }

        scope.Complete(result);

        if (result is not { State: ExecutionState.Completed }) 
            return BridgeMessage.Error(id, result.Error?.Message ?? result.Detail);

        state.RecordCall(tool.Id, tool.ProtocolTool.Name);
        var json = JsonSerializer.SerializeToElement(result.Result);
        return BridgeMessage.Response(id, json);
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
        if (@params?.TryGetProperty(Name, out var nameElement) == true)
            promptName = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(promptName))
            return BridgeMessage.Error(id, "Prompt name is required.");

        toolStore.EnsureLoaded();
        if (!toolStore.TryGetPrompt(null, promptName, out var prompt) || prompt is null)
            return BridgeMessage.Error(id, $"Prompt '{promptName}' is not registered.");

        Dictionary<string, JsonElement>? arguments = null;
        if (@params?.TryGetProperty(Args, out var argsElement) == true)
            arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsElement.GetRawText());

        GetPromptResult result;
        try
        {
            using var cts = new CancellationTokenSource(CallTimeout);
            result = await hostContext
                .ExecuteAsync(() => promptDispatcher.GetPrompt(prompt, arguments), cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return BridgeMessage.Error(id, $"Prompt '{promptName}' exceeded timeout ({CallTimeout.TotalSeconds:F0}s).");
        }

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
        if (@params?.TryGetProperty(Uri, out var uriElement) == true)
            uri = uriElement.GetString();
        if (string.IsNullOrWhiteSpace(uri))
            return BridgeMessage.Error(id, "Resource URI is required.");

        var resolvedUri = uri!;

        toolStore.EnsureLoaded();
        if (!toolStore.TryResolveResourceByUri(resolvedUri, out var resource) || resource is null)
            return BridgeMessage.Error(id, $"Resource '{resolvedUri}' is not registered.");

        ReadResourceResult result;
        try
        {
            using var cts = new CancellationTokenSource(CallTimeout);
            result = await hostContext
                .ExecuteAsync(() => resourceDispatcher.ReadResource(resource, resolvedUri), cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return BridgeMessage.Error(id, $"Resource '{resolvedUri}' exceeded timeout ({CallTimeout.TotalSeconds:F0}s).");
        }

        var json = JsonSerializer.SerializeToElement(result);
        return BridgeMessage.Response(id, json);
    }

    public void ClearCaches()
    {
        toolDispatcher.ClearCache();
        promptDispatcher.ClearCache();
        resourceDispatcher.ClearCache();
        toolsetContextManager.Clear();
    }
}
