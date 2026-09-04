using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Protocol.Invocation;
using DevTools.Mcp.Core.Results;
using ModelContextProtocol.Protocol;
using Microsoft.Extensions.Logging;
using CoreMcpErrorCode = DevTools.Mcp.Core.Results.McpErrorCode;
using RpcErrorCode = ModelContextProtocol.McpErrorCode;
using CapabilitiesKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Capabilities;
using ImplementationKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Implementation;
using ResourcesKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Resources;
using ToolsKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Tools;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Mcp.Adapter.Host;

/// <summary>
/// Spec-first JSON-RPC handler for host MCP (<c>2026-07-28</c>).
/// Clients use <c>server/discover</c> then per-request <c>_meta</c> — no <c>initialize</c> handshake.
/// </summary>
public sealed class McpHandler : IMcpHandler
{
    private readonly McpCatalogStore _catalogStore;
    private readonly IMcpPrimitiveDispatcher _dispatcher;
    private readonly IMcpExecutionTracker _executionTracker;
    private readonly IHostContextExecutor _hostContext;
    private readonly McpHandlerOptions _options;
    private readonly ILogger<McpHandler> _logger;

    public McpHandler(
        McpCatalogStore catalogStore,
        IMcpPrimitiveDispatcher dispatcher,
        IMcpExecutionTracker executionTracker,
        IHostContextExecutor hostContext,
        ILogger<McpHandler> logger,
        McpHandlerOptions? options = null)
    {
        _catalogStore = catalogStore;
        _dispatcher = dispatcher;
        _executionTracker = executionTracker;
        _hostContext = hostContext;
        _logger = logger;
        _options = options ?? new McpHandlerOptions();
    }

    public async ValueTask<JsonObject?> HandleAsync(JsonObject request, CancellationToken cancellationToken = default)
    {
        if (!McpJsonRpc.TryGetMethod(request, out var method) || string.IsNullOrWhiteSpace(method))
            return McpJsonRpc.CreateError(McpJsonRpc.GetId(request), RpcErrorCode.InvalidParams, "Missing JSON-RPC method.");

        var isNotification = !McpJsonRpc.HasId(request);
        var id = McpJsonRpc.GetId(request);
        var parameters = McpJsonRpc.GetParams(request);

        try
        {
            JsonObject? response = method switch
            {
                RequestMethods.Initialize or NotificationMethods.InitializedNotification => RejectLegacyHandshake(id, method),
                RequestMethods.ServerDiscover => HandleServerDiscover(id),
                RequestMethods.Ping => HandlePing(id, parameters),
                RequestMethods.ToolsList => HandleToolsList(id, parameters),
                RequestMethods.ToolsCall => await HandleToolsCallAsync(id, parameters, cancellationToken).ConfigureAwait(false),
                RequestMethods.ResourcesList => HandleResourcesList(id, parameters),
                RequestMethods.ResourcesTemplatesList => HandleResourceTemplatesList(id, parameters),
                RequestMethods.ResourcesRead => await HandleResourcesReadAsync(id, parameters, cancellationToken).ConfigureAwait(false),
                _ when isNotification => null,
                _ => McpJsonRpc.CreateError(id, RpcErrorCode.MethodNotFound, $"Method not found: '{method}'."),
            };

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP handler failed for method '{Method}'", method);
            return McpJsonRpc.CreateError(id, RpcErrorCode.InternalError, ex.Message);
        }
    }

    private static JsonObject RejectLegacyHandshake(JsonNode? id, string method) =>
        McpJsonRpc.CreateError(
            id,
            RpcErrorCode.MethodNotFound,
            $"Method '{method}' is not available on protocol {McpSpecKeys.ProtocolVersions.Current}. Use '{RequestMethods.ServerDiscover}' and per-request metadata.");

    private JsonObject HandleServerDiscover(JsonNode? id) =>
        McpJsonRpc.CreateSuccess(id, CreateDiscoverResult());

    private JsonNode CreateDiscoverResult() =>
        new JsonObject
        {
            [McpSpecKeys.Discover.SupportedVersions] = new JsonArray(McpSpecKeys.ProtocolVersions.Current),
            [McpSpecKeys.Initialize.Capabilities] = new JsonObject
            {
                [CapabilitiesKeys.Tools] = new JsonObject { [CapabilitiesKeys.ListChanged] = true },
                [CapabilitiesKeys.Resources] = new JsonObject
                {
                    [CapabilitiesKeys.ListChanged] = true,
                    [CapabilitiesKeys.Subscribe] = false,
                },
            },
            [McpSpecKeys.Initialize.ServerInfo] = new JsonObject
            {
                [ImplementationKeys.Name] = _options.ServerName,
                [ImplementationKeys.Version] = _options.ServerVersion,
            },
            [McpSpecKeys.Discover.TtlMs] = 0,
            [McpSpecKeys.Discover.CacheScope] = McpSpecKeys.Discover.PrivateCacheScope,
            [McpSpecKeys.Discover.ResultType] = McpSpecKeys.Discover.Complete,
        };

    private JsonObject HandlePing(JsonNode? id, JsonObject? parameters)
    {
        if (RequireCurrentProtocol(id, parameters) is { } error)
            return error;

        return McpJsonRpc.CreateSuccess(id, new JsonObject());
    }

    private JsonObject HandleToolsList(JsonNode? id, JsonObject? parameters)
    {
        if (RequireCurrentProtocol(id, parameters) is { } error)
            return error;

        _catalogStore.EnsureLoaded();
        var result = new ListToolsResult { Tools = _catalogStore.GetToolDescriptors() };
        return McpJsonRpc.CreateSuccess(id, SerializeResult(result));
    }

    private async Task<JsonObject> HandleToolsCallAsync(JsonNode? id, JsonObject? parameters, CancellationToken cancellationToken)
    {
        if (RequireCurrentProtocol(id, parameters) is { } error)
            return error;

        var toolName = parameters?[ToolsKeys.Name]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(toolName))
            return McpJsonRpc.CreateError(id, RpcErrorCode.InvalidParams, "Tool name is required.");

        _catalogStore.EnsureLoaded();
        if (!_catalogStore.TryGetTool(null, toolName, out var registered) || registered is null)
            return McpJsonRpc.CreateError(id, RpcErrorCode.InvalidParams, $"Unknown tool: '{toolName}'.");

        var invocationRequest = InvocationRequestReader.FromWire(parameters);
        invocationRequest.Name = toolName!;

        var invocation = new McpInvocation { ExecutionState = ExecutionState.Queued };
        using var scope = _executionTracker.BeginExecution(toolName!);
        _executionTracker.MarkRunning(scope);
        invocation.ExecutionState = ExecutionState.Running;
        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;

        try
        {
            var dispatchResult = await _dispatcher
                .DispatchToolAsync(registered, invocationRequest, _hostContext, cancellationToken)
                .ConfigureAwait(false);

            if (!dispatchResult.IsSuccess)
            {
                invocation.ExecutionState = ExecutionState.Failed;
                var failed = McpResult<McpInvocationResponse>.Failure(dispatchResult.Error!);
                _executionTracker.Complete(scope, invocation, failed, failed.Error!.Message);
                return McpJsonRpc.CreateSuccess(id, HostToolResultJson.ToNode(new McpInvocationResponse
                {
                    IsError = true,
                    Content = [new McpTextContent(dispatchResult.Error!.Message)],
                }));
            }

            var response = dispatchResult.Value!;
            invocation.ExecutionState = ExecutionState.Completed;
            _executionTracker.Complete(scope, invocation, McpResult<McpInvocationResponse>.Success(response), $"Completed '{toolName}'.");
            if (response.IsError != true)
                _executionTracker.RecordCall(registered.Id, toolName!);

            return McpJsonRpc.CreateSuccess(id, HostToolResultJson.ToNode(response));
        }
        catch (OperationCanceledException)
        {
            invocation.ExecutionState = ExecutionState.Cancelled;
            var cancelled = McpResult<McpInvocationResponse>.Failure(
                new McpError(CoreMcpErrorCode.ExecutionCancelled, $"Tool '{toolName}' was cancelled.", []));
            _executionTracker.Complete(scope, invocation, cancelled, cancelled.Error!.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "tools/call failed for '{ToolName}'", toolName);
            invocation.ExecutionState = ExecutionState.Failed;
            var failed = McpResult<McpInvocationResponse>.Failure(
                new McpError(CoreMcpErrorCode.ExecutionFailed, ex.Message, []));
            _executionTracker.Complete(scope, invocation, failed, ex.Message);
            return McpJsonRpc.CreateSuccess(id, HostToolResultJson.ToNode(new McpInvocationResponse
            {
                IsError = true,
                Content = [new McpTextContent(ex.Message)],
            }));
        }
    }

    private JsonObject HandleResourcesList(JsonNode? id, JsonObject? parameters)
    {
        if (RequireCurrentProtocol(id, parameters) is { } error)
            return error;

        _catalogStore.EnsureLoaded();
        var result = new ListResourcesResult { Resources = _catalogStore.GetResourceDescriptors() };
        return McpJsonRpc.CreateSuccess(id, SerializeResult(result));
    }

    private JsonObject HandleResourceTemplatesList(JsonNode? id, JsonObject? parameters)
    {
        if (RequireCurrentProtocol(id, parameters) is { } error)
            return error;

        _catalogStore.EnsureLoaded();
        var result = new ListResourceTemplatesResult { ResourceTemplates = _catalogStore.GetResourceTemplateDescriptors() };
        return McpJsonRpc.CreateSuccess(id, SerializeResult(result));
    }

    private async Task<JsonObject> HandleResourcesReadAsync(JsonNode? id, JsonObject? parameters, CancellationToken cancellationToken)
    {
        if (RequireCurrentProtocol(id, parameters) is { } error)
            return error;

        var uri = parameters?[ResourcesKeys.Uri]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(uri))
            return McpJsonRpc.CreateError(id, RpcErrorCode.InvalidParams, "Resource URI is required.");

        _catalogStore.EnsureLoaded();
        if (!_catalogStore.TryResolveResourceByUri(uri!, out var registered) || registered is null)
            return McpJsonRpc.CreateError(id, RpcErrorCode.InvalidParams, $"Unknown resource: '{uri}'.");

        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        try
        {
            var result = await _hostContext
                .ExecuteAsync(() => _dispatcher.ReadResource(registered, uri!, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            return McpJsonRpc.CreateSuccess(id, SerializeResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "resources/read failed for '{Uri}'", uri);
            return McpJsonRpc.CreateError(id, RpcErrorCode.InternalError, ex.Message);
        }
    }

    private static JsonObject? RequireCurrentProtocol(JsonNode? id, JsonObject? parameters)
    {
        var version = McpProtocol.GetVersion(parameters);
        if (McpProtocol.IsCurrent(version))
            return null;

        if (string.IsNullOrWhiteSpace(version))
        {
            return McpJsonRpc.CreateError(
                id,
                RpcErrorCode.InvalidParams,
                $"Request params must include _meta/{MetaKeys.ProtocolVersion}.");
        }

        return McpJsonRpc.CreateError(
            id,
            RpcErrorCode.UnsupportedProtocolVersion,
            $"Unsupported protocol version '{version}'.",
            new JsonObject
            {
                ["requested"] = version,
                ["supported"] = new JsonArray(McpSpecKeys.ProtocolVersions.Current),
            });
    }

    private static JsonNode SerializeResult<T>(T result) =>
        JsonSerializer.SerializeToNode(result, ToolHelpers.ProtocolOptions)!;
}
