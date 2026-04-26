using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Core;
using DevTools.McpParser.Models;
using RevitDevTool.ExternalExecution.Mcp.Handlers;
using RevitDevTool.ExternalExecution.Handlers;
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
    PytestRequestHandler pytestRequestHandler) : IHostedService, IDisposable
{
    private const int MaxPipeInstances = 8;
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
            [BridgeMethods.TestsDiscover] = pytestRequestHandler.HandleDiscoverAsync,
            [BridgeMethods.TestsRun] = pytestRequestHandler.HandleRunAsync,
        };

    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private readonly ConcurrentDictionary<int, BridgePipeConnection> _connections = new();
    private int _nextConnectionId;
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
        pytestRequestHandler.NotifySender = SendNotification;

        Task.Run(() =>
        {
            try
            {
                toolStore.EnsureLoaded();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[PipeServer] Catalog preload failed: {ex.Message}");
            }
        }, cancellationToken);

        toolStore.ToolsChanged += OnToolsChanged;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoopTask = AcceptLoopAsync(_cts.Token);

        Trace.TraceInformation($"[PipeServer] Listening on pipe '{_pipeName}'.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        toolStore.ToolsChanged -= OnToolsChanged;

        _cts?.CancelAsync();
        foreach (var connection in _connections.Values)
            connection.Dispose();
        _connections.Clear();

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

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pipe = CreateServerPipe(_pipeName!);
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                RegisterConnection(pipe);
            }
            catch (OperationCanceledException) { break; }
            catch (IOException ex) when (IsPipeInstancesBusy(ex))
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[PipeServer] Accept loop error: {ex.Message}");
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
    }

    private void RegisterConnection(NamedPipeServerStream pipe)
    {
        var conn = new BridgePipeConnection(pipe);
        var connectionId = Interlocked.Increment(ref _nextConnectionId);
        _connections[connectionId] = conn;
        state.SetConnectedState(_connections.IsEmpty ? 0 : 1);
        Trace.TraceInformation($"[PipeServer] Client connected. Active clients: {_connections.Count}");

        conn.MessageReceived += msg => OnMessageReceived(conn, msg);
        conn.Disconnected += () =>
        {
            if (_connections.TryRemove(connectionId, out var disconnectedConnection))
                disconnectedConnection.Dispose();
            state.SetConnectedState(_connections.IsEmpty ? 0 : 1);
            Trace.TraceInformation($"[PipeServer] Client disconnected. Active clients: {_connections.Count}");
        };
        conn.StartReadLoop();
    }

    private async void OnMessageReceived(BridgePipeConnection connection, BridgeMessage msg)
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
                await connection.WriteAsync(response).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[PipeServer] Failed to send response: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[PipeServer] Unhandled error in message handler: {ex}");
        }
    }

    private async Task<BridgeMessage> HandleRequestAsync(BridgeMessage request)
    {
        var id = request.Id!;
        if (Handlers.TryGetValue(request.Method!, out var handler))
            return await handler(id, request.Params).ConfigureAwait(false);
        return BridgeMessage.Error(id, $"Unknown method: {request.Method}");
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
            if (_connections.IsEmpty) return;

            var @params = data is not null ? JsonSerializer.SerializeToElement(data) : (JsonElement?)null;
            var notification = BridgeMessage.Notification(method, @params);

            foreach (var connection in _connections.Values)
            {
                try
                {
                    await connection.WriteAsync(notification).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"[PipeServer] Notification '{method}' failed: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[PipeServer] Unhandled error in SendNotification: {ex}");
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
        return new NamedPipeServerStream(pipeName, PipeDirection.InOut, MaxPipeInstances,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
#else
        return NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, MaxPipeInstances,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
#endif
    }

    private static bool IsPipeInstancesBusy(IOException ex)
    {
        const int allPipeInstancesBusy = 231;
        var win32Code = ex.HResult & 0xFFFF;
        return win32Code == allPipeInstancesBusy ||
               ex.Message.Contains("All pipe instances are busy", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        toolStore.ToolsChanged -= OnToolsChanged;
        _cts?.Cancel();
        foreach (var connection in _connections.Values)
            connection.Dispose();
        _connections.Clear();
        _cts?.Dispose();
    }
}
