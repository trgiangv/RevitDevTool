using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using RevitDevTool.Contracts;
using RevitDevTool.Controllers;
using RevitDevTool.Core;
using RevitDevTool.Mcp.Models;
using RevitDevTool.Utils;

namespace RevitDevTool.Mcp;

[UsedImplicitly]
public sealed class RevitPipeServer(
    ToolRegistryStore toolStore,
    BridgeConnectionState state,
    ToolExecutionDispatcher dispatcher,
    PrimitiveExecutionDispatcher primitiveDispatcher) : IHostedService, IDisposable
{
    private CancellationTokenSource? _cts;
    private volatile BridgePipeConnection? _connection;
    private string? _pipeName;
    private bool _disposed;

    private string _documentTitle = string.Empty;
    private string _documentPath = string.Empty;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null) return Task.CompletedTask;

        var pid = SettingsUtils.CurrentProcessId;
        _pipeName = $"Revit_{GetRevitVersion()}_{pid}";
        state.SetEndpoint(_pipeName);
        state.SetConnectedState(0);
        state.SetQueueDepth(0);

        try
        {
            var doc = RevitContext.ActiveDocument;
            if (doc is not null)
            {
                _documentTitle = doc.Title ?? string.Empty;
                _documentPath = doc.PathName ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[MCP/PIPE] Could not read active document: {ex.Message}");
        }

        _ = Task.Run(() =>
        {
            try { toolStore.EnsureLoaded(); }
            catch (Exception ex) { Trace.TraceWarning($"[MCP/PIPE] Catalog preload failed: {ex.Message}"); }
        }, cancellationToken);

        toolStore.ToolsChanged += OnToolsChanged;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = AcceptLoopAsync(_cts.Token);

        Trace.TraceInformation($"[MCP/PIPE] Listening on pipe '{_pipeName}'.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        toolStore.ToolsChanged -= OnToolsChanged;

        _cts?.Cancel();
        _connection?.Dispose();
        _connection = null;
        _cts?.Dispose();
        _cts = null;

        state.SetConnectedState(0);
        state.SetQueueDepth(0);
        return Task.CompletedTask;
    }

    public void UpdateDocumentInfo(string title, string path)
    {
        _documentTitle = title;
        _documentPath = path;
        SendNotification(BridgeMethods.NotifyDocumentChanged, BuildInstanceInfo());
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pipe = CreateServerPipe(_pipeName!);
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);

                state.SetConnectedState(1);
                Trace.TraceInformation("[MCP/PIPE] Client connected.");

                var disconnectSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var conn = new BridgePipeConnection(pipe);
                _connection = conn;

                conn.MessageReceived += msg => OnMessageReceived(conn, msg);
                conn.Disconnected += () =>
                {
                    _connection = null;
                    state.SetConnectedState(0);
                    Trace.TraceInformation("[MCP/PIPE] Client disconnected.");
                    disconnectSignal.TrySetResult(true);
                };
                conn.StartReadLoop();

                await disconnectSignal.Task.ConfigureAwait(false);
                conn.Dispose();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[MCP/PIPE] Accept loop error: {ex.Message}");
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
    }

    private async void OnMessageReceived(BridgePipeConnection conn, BridgeMessage msg)
    {
        if (msg is not { Type: BridgeMessage.TypeRequest, Id: not null, Method: not null })
            return;

        BridgeMessage response;
        try
        {
            response = await HandleRequestAsync(msg).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            response = BridgeMessage.Error(msg.Id, ex.Message);
        }

        try
        {
            await conn.WriteAsync(response).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[MCP/PIPE] Failed to send response: {ex.Message}");
        }
    }

    private async Task<BridgeMessage> HandleRequestAsync(BridgeMessage request)
    {
        var id = request.Id!;
        return request.Method switch
        {
            BridgeMethods.ToolsList => HandleToolsList(id),
            BridgeMethods.ToolsCall => await HandleToolsCallAsync(id, request.Params).ConfigureAwait(false),
            BridgeMethods.PromptsList => HandlePromptsList(id),
            BridgeMethods.PromptsGet => await HandlePromptsGetAsync(id, request.Params).ConfigureAwait(false),
            BridgeMethods.ResourcesList => HandleResourcesList(id),
            BridgeMethods.ResourceTemplatesList => HandleResourceTemplatesList(id),
            BridgeMethods.ResourcesRead => await HandleResourcesReadAsync(id, request.Params).ConfigureAwait(false),
            BridgeMethods.InstanceInfo => HandleInstanceInfo(id),
            _ => BridgeMessage.Error(id, $"Unknown method: {request.Method}")
        };
    }

    private BridgeMessage HandleToolsList(string id)
    {
        toolStore.EnsureLoaded();
        var tools = toolStore.Tools.ToList();
        var json = JsonSerializer.SerializeToElement(tools);
        return BridgeMessage.Response(id, json);
    }

    private async Task<BridgeMessage> HandleToolsCallAsync(string id, JsonElement? @params)
    {
        var toolName = @params?.GetProperty("name").GetString();
        if (string.IsNullOrWhiteSpace(toolName))
            return BridgeMessage.Error(id, "Tool name is required.");

        toolStore.EnsureLoaded();
        if (!toolStore.TryGetTool(null, toolName, out var tool) || tool is null)
            return BridgeMessage.Error(id, $"Tool '{toolName}' is not registered.");

        var payloadJson = "{}";
        if (@params?.TryGetProperty("arguments", out var argsElement) == true)
            payloadJson = argsElement.GetRawText();

        var handler = await ExternalEventController
            .AsyncGenericEventHandler<McpToolExecutionResult>()
            .ConfigureAwait(false);

        var result = await handler
            .RaiseAsync(() => dispatcher.Dispatch(tool, payloadJson))
            .ConfigureAwait(false);

        if (result is { State: ExecutionState.Completed })
        {
            state.RecordCall(tool.Id, tool.ProtocolTool.Name);
            var json = JsonSerializer.SerializeToElement(result.Result);
            return BridgeMessage.Response(id, json);
        }

        return BridgeMessage.Error(id, result.Error?.Message ?? result.Detail);
    }

    private BridgeMessage HandlePromptsList(string id)
    {
        toolStore.EnsureLoaded();
        var prompts = toolStore.Prompts.ToList();
        var json = JsonSerializer.SerializeToElement(prompts);
        return BridgeMessage.Response(id, json);
    }

    private async Task<BridgeMessage> HandlePromptsGetAsync(string id, JsonElement? @params)
    {
        var promptName = @params?.GetProperty("name").GetString();
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

        var result = await handler
            .RaiseAsync(() => primitiveDispatcher.GetPrompt(prompt, arguments))
            .ConfigureAwait(false);

        var json = JsonSerializer.SerializeToElement(result);
        return BridgeMessage.Response(id, json);
    }

    private BridgeMessage HandleResourcesList(string id)
    {
        toolStore.EnsureLoaded();
        var resources = toolStore.DirectResources.ToList();
        var json = JsonSerializer.SerializeToElement(resources);
        return BridgeMessage.Response(id, json);
    }

    private BridgeMessage HandleResourceTemplatesList(string id)
    {
        toolStore.EnsureLoaded();
        var templates = toolStore.ResourceTemplates.ToList();
        var json = JsonSerializer.SerializeToElement(templates);
        return BridgeMessage.Response(id, json);
    }

    private async Task<BridgeMessage> HandleResourcesReadAsync(string id, JsonElement? @params)
    {
        var uri = @params?.GetProperty("uri").GetString();
        if (string.IsNullOrWhiteSpace(uri))
            return BridgeMessage.Error(id, "Resource URI is required.");

        toolStore.EnsureLoaded();
        var resource = toolStore.ResourceCatalog.FirstOrDefault(candidate =>
        {
            var candidateUri = candidate.ProtocolTemplate?.UriTemplate ?? candidate.ProtocolResource?.Uri ?? string.Empty;
            return string.Equals(candidateUri, uri, StringComparison.OrdinalIgnoreCase);
        });

        if (resource is null)
            return BridgeMessage.Error(id, $"Resource '{uri}' is not registered.");

        var handler = await ExternalEventController
            .AsyncGenericEventHandler<ReadResourceResult>()
            .ConfigureAwait(false);

        var result = await handler
            .RaiseAsync(() => primitiveDispatcher.ReadResource(resource, uri!))
            .ConfigureAwait(false);

        var json = JsonSerializer.SerializeToElement(result);
        return BridgeMessage.Response(id, json);
    }

    private BridgeMessage HandleInstanceInfo(string id)
    {
        var json = JsonSerializer.SerializeToElement(BuildInstanceInfo());
        return BridgeMessage.Response(id, json);
    }

    private InstanceInfo BuildInstanceInfo() => new()
    {
        ProcessId = SettingsUtils.CurrentProcessId,
        VersionNumber = GetRevitVersion(),
        DocumentTitle = _documentTitle,
        DocumentPath = _documentPath
    };

    private void OnToolsChanged(object? sender, EventArgs e)
    {
        SendNotification(BridgeMethods.NotifyToolsChanged);
    }

    private async void SendNotification(string method, object? data = null)
    {
        var conn = _connection;
        if (conn is null) return;

        var @params = data is not null ? JsonSerializer.SerializeToElement(data) : (JsonElement?)null;
        var notification = BridgeMessage.Notification(method, @params);

        try
        {
            await conn.WriteAsync(notification).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[MCP/PIPE] Notification '{method}' failed: {ex.Message}");
        }
    }

    private static string GetRevitVersion() => RevitContext.Application.VersionNumber;

    private static NamedPipeServerStream CreateServerPipe(string pipeName)
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

#if NETFRAMEWORK
        return new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
#else
        return NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
#endif
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        toolStore.ToolsChanged -= OnToolsChanged;
        _cts?.Cancel();
        _connection?.Dispose();
        _cts?.Dispose();
    }
}
