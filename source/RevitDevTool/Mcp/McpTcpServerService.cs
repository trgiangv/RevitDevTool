using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using RevitDevTool.Contracts;
using RevitDevTool.Controllers;
using RevitDevTool.Mcp.Models;
using RevitDevTool.Mcp.Parser.Models;

namespace RevitDevTool.Mcp;

[UsedImplicitly]
public sealed class McpTcpServerService(
    McpToolStore toolStore,
    McpExecutionQueue executionQueue,
    McpBridgeState state,
    McpToolExecutionDispatcher dispatcher,
    McpPrimitiveExecutionDispatcher primitiveDispatcher) : IHostedService, IDisposable
{
    private const int DefaultPort = 18080;
    private TcpListener? _listener;
    private CancellationTokenSource? _serverCts;
    private Task? _acceptLoopTask;

    private readonly ConcurrentDictionary<string, NetworkStream> _clients = new();
    private int _port;
    private bool _disposed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_listener is not null)
            return Task.CompletedTask;

        _ = Task.Run(() =>
        {
            try { toolStore.EnsureLoaded(); }
            catch (Exception ex) { Trace.TraceWarning($"[MCP] Catalog preload failed: {ex.Message}"); }
        }, cancellationToken);

        state.SetConnectedState(0);
        state.SetQueueDepth(0);

        _port = FindAvailablePort(DefaultPort);

        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();

        state.SetEndpoint(_port.ToString());
        Trace.TraceInformation($"[MCP/TCP] Listening on {_port}");

        toolStore.ToolsChanged += OnToolsChanged;

        _serverCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoopTask = RunAcceptLoopAsync(_serverCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_listener is null)
            return;

        toolStore.ToolsChanged -= OnToolsChanged;

        foreach (var key in _clients.Keys.ToList())
            _clients.TryRemove(key, out _);

        state.SetConnectedState(0);
        state.SetQueueDepth(0);

        _serverCts?.Cancel();
        _listener.Stop();

        if (_acceptLoopTask is not null)
        {
            try
            {
#if NET
                await _acceptLoopTask.WaitAsync(cancellationToken).ConfigureAwait(false);
#else
                await _acceptLoopTask.ConfigureAwait(false);
#endif
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[MCP/TCP] Accept loop shutdown warning: {ex.Message}");
            }
        }

        _serverCts?.Dispose();
        _serverCts = null;
        _acceptLoopTask = null;
        _listener = null;
    }

    private void OnToolsChanged(object? sender, EventArgs e)
    {
        _ = BroadcastToolsChangedAsync();
    }

    private async Task BroadcastToolsChangedAsync()
    {
        var envelope = new Envelope
        {
            Kind = BridgeMessageKinds.Event,
            Action = BridgeActions.ToolsChanged,
            Body = BridgeFrameCodec.SerializeBody(new McpToolsChangedEventBody())
        };

        foreach (var kvp in _clients.ToArray())
        {
            try
            {
                await BridgeFrameCodec.WriteEnvelopeAsync(kvp.Value, envelope, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[MCP/TCP] Failed to push tools.changed to client {kvp.Key}: {ex.Message}");
                _clients.TryRemove(kvp.Key, out _);
                state.SetConnectedState(_clients.Count);
            }
        }
    }

    private async Task RunAcceptLoopAsync(CancellationToken cancellationToken)
    {
        if (_listener is null)
            return;

        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
#if NET           
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
#else
                client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
#endif
                var acceptedClient = client;
                var clientId = Guid.NewGuid().ToString("N");
                var stream = acceptedClient.GetStream();
                _clients[clientId] = stream;

                state.SetConnectedState(_clients.Count);
                Trace.TraceInformation($"[MCP/TCP] Client connected. Active clients: {_clients.Count}");
                _ = HandleClientAsync(clientId, acceptedClient, stream, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                client?.Dispose();
                Trace.TraceError($"[MCP/TCP] Accept error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    private async Task HandleClientAsync(string clientId, TcpClient client, NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                var request = await BridgeFrameCodec.ReadEnvelopeAsync(stream, cancellationToken).ConfigureAwait(false);
                if (request is null)
                    break;

                var response = await HandleRequestAsync(request, cancellationToken).ConfigureAwait(false);
                await BridgeFrameCodec.WriteEnvelopeAsync(stream, response, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException ex)
        {
            Trace.TraceWarning($"[MCP/TCP] Client {clientId} connection lost: {ex.Message}");
        }
        catch (SocketException ex)
        {
            Trace.TraceWarning($"[MCP/TCP] Client {clientId} socket error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[MCP/TCP] Client handler error: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            try { client.Close(); } catch { /* ignored */ }

            state.SetConnectedState(_clients.Count);
            Trace.TraceInformation($"[MCP/TCP] Client disconnected. Active clients: {_clients.Count}");
        }
    }


    private async Task<Envelope> HandleRequestAsync(Envelope message, CancellationToken cancellationToken)
    {
        return message.Action switch
        {
            BridgeActions.Ping => BuildOk(message, BridgeActions.Pong, new McpBridgePongBody
            {
                Endpoint = state.Endpoint,
                Port = _port
            }),
            BridgeActions.ListTools => HandleListTools(message),
            BridgeActions.ListPrompts => HandleListPrompts(message),
            BridgeActions.ListResources => HandleListResources(message),
            BridgeActions.ToolCall => await HandleToolCallAsync(message, cancellationToken).ConfigureAwait(false),
            BridgeActions.GetPrompt => await HandlePromptGetAsync(message, cancellationToken).ConfigureAwait(false),
            BridgeActions.ReadResource => await HandleResourceReadAsync(message, cancellationToken).ConfigureAwait(false),
            BridgeActions.GetExecution => HandleGetExecution(message),
            BridgeActions.CancelExecution => HandleCancelExecution(message),
            BridgeActions.Shutdown => HandleShutdown(message),
            _ => BuildError(message, BridgeErrorCodes.UnknownAction, $"Unknown action '{message.Action}'")
        };
    }

    private Envelope HandleListTools(Envelope message)
    {
        toolStore.EnsureLoaded();

        return BuildOk(message, BridgeActions.ListTools, new McpToolsListResponseBody
        {
            Tools = toolStore.Tools.ToList()
        });
    }

    private Envelope HandleListPrompts(Envelope message)
    {
        toolStore.EnsureLoaded();

        return BuildOk(message, BridgeActions.ListPrompts, new McpPromptsListResponseBody
        {
            Prompts = toolStore.Prompts.ToList()
        });
    }

    private Envelope HandleListResources(Envelope message)
    {
        toolStore.EnsureLoaded();

        return BuildOk(message, BridgeActions.ListResources, new McpResourcesListResponseBody
        {
            Resources = toolStore.DirectResources.ToList(),
            ResourceTemplates = toolStore.ResourceTemplates.ToList()
        });
    }

    private Envelope HandleShutdown(Envelope message)
    {
        state.SetQueueDepth(0);
        return BuildOk(message, BridgeActions.Shutdown, new McpShutdownResponseBody());
    }

    private async Task<Envelope> HandleToolCallAsync(Envelope message, CancellationToken cancellationToken)
    {
        var requestId = string.IsNullOrWhiteSpace(message.Id) ? "<no-id>" : message.Id;
        var requestBody = BridgeFrameCodec.ReadBody<McpToolCallRequestBody>(message);
        if (requestBody is null)
            return BuildError(message, BridgeErrorCodes.ToolInvalidRequest, "Tool call body is required.");

        if (string.IsNullOrWhiteSpace(requestBody.ToolId) && string.IsNullOrWhiteSpace(requestBody.ToolName))
            return BuildError(message, BridgeErrorCodes.ToolMissingName, "ToolId or ToolName is required.");

        toolStore.EnsureLoaded();

        var requestedTool = !string.IsNullOrWhiteSpace(requestBody.ToolId) ? requestBody.ToolId! : requestBody.ToolName!;
        Trace.TraceInformation($"[MCP/TCP] Tool call received. requestId={requestId}, tool={requestedTool}");
        if (!toolStore.TryGetTool(requestBody.ToolId, requestBody.ToolName, out var tool) || tool is null)
            return BuildError(message, BridgeErrorCodes.ToolNotFound, $"Tool '{requestedTool}' is not registered.");

        var executionId = string.IsNullOrWhiteSpace(message.ExecutionId)
            ? Guid.NewGuid().ToString("N")
            : message.ExecutionId;

        var result = await executionQueue.EnqueueAsync(
            executionId,
            tool.Id,
            tool.ProtocolTool.Name,
            (progress, token) => ExecuteOnRevitThreadAsync(requestBody, tool, progress, token),
            cancellationToken).ConfigureAwait(false);

        if (result.State == ExecutionState.Completed)
        {
            state.RecordCall(tool.Id, tool.ProtocolTool.Name);
            Trace.TraceInformation($"[MCP/TCP] Tool call succeeded. requestId={requestId}, tool={tool.Id}");
            return BuildOk(message, BridgeActions.ToolCall, new McpToolCallResponseBody
            {
                ToolId = tool.Id,
                ToolName = tool.ProtocolTool.Name,
                State = result.State,
                Detail = result.Detail,
                Result = result.Result,
            }, executionId);
        }

        Trace.TraceWarning(
            $"[MCP/TCP] Tool call failed. requestId={requestId}, tool={tool.Id}, code={result.Error?.Code}, message={result.Error?.Message}");
        return BuildError(message, result.Error?.Code ?? BridgeErrorCodes.ToolFailed, result.Error?.Message ?? result.Detail, executionId, result.Error?.Details);
    }

    private async Task<Envelope> HandlePromptGetAsync(Envelope message, CancellationToken cancellationToken)
    {
        var requestBody = BridgeFrameCodec.ReadBody<McpPromptGetRequestBody>(message);
        if (requestBody is null)
            return BuildError(message, BridgeErrorCodes.PromptInvalidRequest, "Prompt get body is required.");

        if (string.IsNullOrWhiteSpace(requestBody.PromptId) && string.IsNullOrWhiteSpace(requestBody.PromptName))
            return BuildError(message, BridgeErrorCodes.PromptMissingName, "PromptId or PromptName is required.");

        toolStore.EnsureLoaded();
        if (!toolStore.TryGetPrompt(requestBody.PromptId, requestBody.PromptName, out var prompt) || prompt is null)
        {
            var requestedPrompt = !string.IsNullOrWhiteSpace(requestBody.PromptId) ? requestBody.PromptId! : requestBody.PromptName!;
            return BuildError(message, BridgeErrorCodes.PromptNotFound, $"Prompt '{requestedPrompt}' is not registered.");
        }

        try
        {
            var result = await ExecutePromptOnRevitThreadAsync(prompt, requestBody.Arguments, cancellationToken).ConfigureAwait(false);
            return BuildOk(message, BridgeActions.GetPrompt, new McpPromptGetResponseBody
            {
                PromptId = prompt.Id,
                PromptName = prompt.ProtocolPrompt.Name,
                Result = result,
            });
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[MCP/TCP] Prompt get failed for '{prompt.ProtocolPrompt.Name}': {ex.Message}");
            return BuildError(message, BridgeErrorCodes.PromptInvokeFailed, ex.Message, details: ex.StackTrace);
        }
    }

    private async Task<Envelope> HandleResourceReadAsync(Envelope message, CancellationToken cancellationToken)
    {
        var requestBody = BridgeFrameCodec.ReadBody<McpResourceReadRequestBody>(message);
        if (requestBody is null)
            return BuildError(message, BridgeErrorCodes.ResourceInvalidRequest, "Resource read body is required.");
        if (string.IsNullOrWhiteSpace(requestBody.Uri))
            return BuildError(message, BridgeErrorCodes.ResourceMissingUri, "Resource URI is required.");

        toolStore.EnsureLoaded();
        if (!TryResolveResource(requestBody, out var resource) || resource is null)
            return BuildError(message, BridgeErrorCodes.ResourceNotFound, $"Resource '{requestBody.Uri}' is not registered.");

        try
        {
            var result = await ExecuteResourceOnRevitThreadAsync(resource, requestBody.Uri, cancellationToken).ConfigureAwait(false);
            var resourceName = resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name ?? string.Empty;
            return BuildOk(message, BridgeActions.ReadResource, new McpResourceReadResponseBody
            {
                ResourceId = resource.Id,
                ResourceName = resourceName,
                Result = result,
            });
        }
        catch (Exception ex)
        {
            var resourceName = resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name ?? string.Empty;
            Trace.TraceWarning($"[MCP/TCP] Resource read failed for '{resourceName}': {ex.Message}");
            return BuildError(message, BridgeErrorCodes.ResourceReadFailed, ex.Message, details: ex.StackTrace);
        }
    }

    private Envelope HandleGetExecution(Envelope message)
    {
        if (string.IsNullOrWhiteSpace(message.ExecutionId))
            return BuildError(message, BridgeErrorCodes.ExecutionMissingId, "ExecutionId is required.");

        var snapshot = executionQueue.GetExecutionSnapshot(message.ExecutionId);
        if (snapshot is null)
            return BuildError(message, BridgeErrorCodes.ExecutionNotFound, $"Execution '{message.ExecutionId}' was not found.");

        return BuildExecutionEnvelope(message, BridgeActions.GetExecution, snapshot);
    }

    private Envelope HandleCancelExecution(Envelope message)
    {
        if (string.IsNullOrWhiteSpace(message.ExecutionId))
            return BuildError(message, BridgeErrorCodes.ExecutionMissingId, "ExecutionId is required.");

        if (!executionQueue.TryCancel(message.ExecutionId))
            return BuildError(message, BridgeErrorCodes.ExecutionNotFound, $"Execution '{message.ExecutionId}' was not found or cannot be cancelled.");

        var snapshot = executionQueue.GetExecutionSnapshot(message.ExecutionId);
        if (snapshot is null)
            return BuildError(message, BridgeErrorCodes.ExecutionNotFound, $"Execution '{message.ExecutionId}' was not found.");

        return BuildExecutionEnvelope(message, BridgeActions.CancelExecution, snapshot);
    }

    private Task<McpToolExecutionResult> ExecuteOnRevitThreadAsync(
        McpToolCallRequestBody requestBody,
        McpRegisteredTool tool,
        IProgress<McpProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        return dispatcher.DispatchAsync(tool, requestBody.PayloadJson, progress, cancellationToken);
    }

    private async Task<GetPromptResult> ExecutePromptOnRevitThreadAsync(
        McpRegisteredPrompt prompt,
        IReadOnlyDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken)
    {
        var handler = await ExternalEventController
            .AsyncGenericEventHandler<GetPromptResult>()
            .ConfigureAwait(false);
        return await handler
            .RaiseAsync(() => primitiveDispatcher.GetPromptAsync(prompt, arguments, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult())
            .ConfigureAwait(false);
    }

    private async Task<ReadResourceResult> ExecuteResourceOnRevitThreadAsync(
        McpRegisteredResource resource,
        string uri,
        CancellationToken cancellationToken)
    {
        var handler = await ExternalEventController
            .AsyncGenericEventHandler<ReadResourceResult>()
            .ConfigureAwait(false);
        return await handler
            .RaiseAsync(() => primitiveDispatcher.ReadResourceAsync(resource, uri, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult())
            .ConfigureAwait(false);
    }

    private bool TryResolveResource(McpResourceReadRequestBody requestBody, out McpRegisteredResource? resource)
    {
        if (toolStore.TryGetResource(requestBody.ResourceId, requestBody.ResourceName, out resource) && resource is not null)
            return true;

        var requestUri = requestBody.Uri;
        resource = toolStore.ResourceCatalog.FirstOrDefault(candidate =>
        {
            var candidateUri = candidate.ProtocolTemplate?.UriTemplate ?? candidate.ProtocolResource?.Uri ?? string.Empty;
            return string.Equals(candidateUri, requestUri, StringComparison.OrdinalIgnoreCase);
        });
        return resource is not null;
    }

    private static Envelope BuildOk<TBody>(
        Envelope message,
        string action,
        TBody body,
        string? executionId = null)
    {
        return new Envelope
        {
            Id = message.Id,
            ExecutionId = executionId ?? message.ExecutionId,
            Kind = BridgeMessageKinds.Response,
            Action = action,
            Body = BridgeFrameCodec.SerializeBody(body)
        };
    }

    private static Envelope BuildError(Envelope message, string code, string reason, string? executionId = null, string? details = null)
    {
        return new Envelope
        {
            Id = message.Id,
            ExecutionId = executionId ?? message.ExecutionId,
            Kind = BridgeMessageKinds.Response,
            Action = message.Action,
            IsError = true,
            Body = BridgeFrameCodec.SerializeBody(new McpErrorBody { Code = code, Message = reason, Details = details })
        };
    }

    private static Envelope BuildExecutionEnvelope(Envelope message, string action, McpExecutionSnapshot snapshot)
    {
        return BuildOk(message, action, new McpExecutionResponseBody { Execution = snapshot }, snapshot.ExecutionId);
    }


    private static int FindAvailablePort(int preferredPort)
    {
        try
        {
            var tester = new TcpListener(IPAddress.Loopback, preferredPort);
            tester.Start();
            tester.Stop();
            return preferredPort;
        }
        catch (SocketException)
        {
            var fallback = new TcpListener(IPAddress.Loopback, 0);
            fallback.Start();
            var port = ((IPEndPoint)fallback.LocalEndpoint).Port;
            fallback.Stop();
            return port;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        toolStore.ToolsChanged -= OnToolsChanged;
        _listener?.Stop();
        _serverCts?.Cancel();
        _serverCts?.Dispose();
    }
}
