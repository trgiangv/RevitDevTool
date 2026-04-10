using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Controllers;
using RevitDevTool.Core;
using RevitDevTool.McpParser.Models;
using RevitDevTool.ExternalExecution.Mcp.Handlers;
using RevitDevTool.ExternalExecution.Handlers;
using RevitDevTool.ExternalExecution.Testing;
using RevitDevTool.ExternalExecution.Mcp;
using RevitDevTool.ExternalExecution.Connections;
// ReSharper disable RedundantSuppressNullableWarningExpression
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
// ReSharper disable ReplaceWithFieldKeyword

namespace RevitDevTool.ExternalExecution;

[UsedImplicitly]
public sealed class RevitPipeServer(
    ToolRegistryStore toolStore,
    ConnectionState state,
    InstanceRequestHandler instanceRequestHandler,
    RegistryRequestHandler registryRequestHandler,
    TestExecutionService testExecutionService) : IHostedService, IDisposable
{
    private Dictionary<string, Func<string, JsonElement?, Task<BridgeMessage>>>? _handlers;

    private Dictionary<string, Func<string, JsonElement?, Task<BridgeMessage>>> Handlers =>
        _handlers ??= new Dictionary<string, Func<string, JsonElement?, Task<BridgeMessage>>>(StringComparer.OrdinalIgnoreCase)
        {
            [BridgeMethods.ToolsList] = (id, _) => registryRequestHandler.HandleToolsListAsync(id),
            [BridgeMethods.ToolsCall] = registryRequestHandler.HandleToolsCallAsync,
            [BridgeMethods.PromptsList] = (id, _) => registryRequestHandler.HandlePromptsListAsync(id),
            [BridgeMethods.PromptsGet] = registryRequestHandler.HandlePromptsGetAsync,
            [BridgeMethods.ResourcesList] = (id, _) => registryRequestHandler.HandleResourcesListAsync(id),
            [BridgeMethods.ResourceTemplatesList] = (id, _) => registryRequestHandler.HandleResourceTemplatesListAsync(id),
            [BridgeMethods.ResourcesRead] = registryRequestHandler.HandleResourcesReadAsync,
            [BridgeMethods.InstanceInfo] = (id, _) => Task.FromResult(instanceRequestHandler.HandleInstanceInfo(id)),
            [BridgeMethods.TestsExecute] = HandleTestsExecuteAsync,
        };

    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private volatile BridgePipeConnection? _connection;
    private string? _pipeName;
    private bool _disposed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null) return Task.CompletedTask;
        _pipeName = $"Revit_{RevitContext.Application.VersionNumber}_{Environment.ProcessId}";
        state.SetEndpoint(_pipeName);
        state.SetConnectedState(0);
        state.SetQueueDepth(0);
        instanceRequestHandler.InitializeFromContext();

        _ = Task.Run(() =>
        {
            try { toolStore.EnsureLoaded(); }
            catch (Exception ex) { Trace.TraceWarning($"[MCP/PIPE] Catalog preload failed: {ex.Message}"); }
        }, cancellationToken);

        toolStore.ToolsChanged += OnToolsChanged;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoopTask = AcceptLoopAsync(_cts.Token);

        Trace.TraceInformation($"[MCP/PIPE] Listening on pipe '{_pipeName}'.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        toolStore.ToolsChanged -= OnToolsChanged;

        _cts?.CancelAsync();
        _connection?.Dispose();
        _connection = null;

        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
        }

        _acceptLoopTask = null;
        _cts?.Dispose();
        _cts = null;

        state.SetConnectedState(0);
        state.SetQueueDepth(0);
    }

    public void UpdateDocumentInfo(string title, string path)
    {
        instanceRequestHandler.UpdateDocumentInfo(title, path);
        SendNotification(BridgeMethods.NotifyDocumentChanged, instanceRequestHandler.BuildInstanceInfo());
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

                conn.MessageReceived += OnMessageReceived;
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

    private async void OnMessageReceived(BridgeMessage msg)
    {
        try
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
                var conn = _connection;
                if (conn is not null)
                    await conn.WriteAsync(response).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[MCP/PIPE] Failed to send response: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[MCP/PIPE] Unhandled error in message handler: {ex}");
        }
    }

    private async Task<BridgeMessage> HandleRequestAsync(BridgeMessage request)
    {
        var id = request.Id!;
        if (Handlers.TryGetValue(request.Method!, out var handler))
            return await handler(id, request.Params).ConfigureAwait(false);
        return BridgeMessage.Error(id, $"Unknown method: {request.Method}");
    }

    private async Task<BridgeMessage> HandleTestsExecuteAsync(string id, JsonElement? @params)
    {
        if (!TestExecutionService.TryParseRequest(@params, out var request, out var error))
            return BridgeMessage.Error(id, error ?? "Invalid test execution request.");

        var handler = await ExternalEventController
            .AsyncGenericEventHandler<TestExecutionResponse>()
            .ConfigureAwait(false);

        var result = await handler.RaiseAsync(() => testExecutionService.Execute(request!)).ConfigureAwait(false);
        var json = JsonSerializer.SerializeToElement(result);
        return BridgeMessage.Response(id, json);
    }

    private void OnToolsChanged(object? sender, EventArgs e)
    {
        registryRequestHandler.ClearCaches();
        SendNotification(BridgeMethods.NotifyToolsChanged);
    }

    private async void SendNotification(string method, object? data = null)
    {
        try
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
        catch (Exception ex)
        {
            Trace.TraceError($"[MCP/PIPE] Unhandled error in SendNotification: {ex}");
        }
    }

    private static NamedPipeServerStream CreateServerPipe(string pipeName)
    {
        var security = new PipeSecurity();
        var currentUser = WindowsIdentity.GetCurrent();
        if (currentUser.User is null)
            throw new InvalidOperationException("Cannot determine current user SID for pipe ACL.");

        security.AddAccessRule(new PipeAccessRule(
            currentUser.User,
            PipeAccessRights.FullControl,
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
