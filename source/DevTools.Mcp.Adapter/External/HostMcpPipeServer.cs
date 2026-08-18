using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using DevTools.Mcp.Adapter.Host;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ZLogger;
namespace DevTools.Mcp.Adapter.External;

/// <summary>
/// Host-side MCP server over <c>DevToolsMcp_{Host}_{Version}_{PID}</c> using the host MCP handler.
/// The <see cref="DevToolsPipeServer"/> remains for pytest/control IPC.
/// </summary>
[UsedImplicitly]
public sealed class HostMcpPipeServer(
    McpCatalogStore catalogStore,
    IMcpPrimitiveDispatcher primitiveDispatcher,
    IMcpPipeConnectionTracker connectionTracker,
    McpToolsetContextManager toolsetContextManager,
    IHostAppInfo hostInfo,
    IMcpHandler mcpHandler,
    ILogger<HostMcpPipeServer> logger) : IHostedService, IDisposable
{
    private const int MaxPipeInstances = 8;

    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private readonly ConcurrentDictionary<int, McpPipeSession> _sessions = new();
    private int _nextSessionId;
    private string? _pipeName;
    private bool _disposed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
            return Task.CompletedTask;

        _pipeName = HostPipeName.FormatMcp(hostInfo.Host.ToString(), hostInfo.VersionNumber, Environment.ProcessId);
        connectionTracker.SetMcpEndpoint(_pipeName);

        Task.Run(() =>
        {
            try
            {
                catalogStore.EnsureLoaded();
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"MCP catalog preload failed: {ex.Message}");
            }
        }, cancellationToken);

        catalogStore.CatalogChanged += OnCatalogChanged;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoopTask = AcceptLoopAsync(_cts.Token);

        logger.ZLogInformation($"MCP listening on pipe '{_pipeName}' (host MCP handler).");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        catalogStore.CatalogChanged -= OnCatalogChanged;
        _cts?.Cancel();

        foreach (var session in _sessions.Values)
            await session.DisposeAsync().ConfigureAwait(false);
        _sessions.Clear();
        connectionTracker.ClearMcpState();

        if (_acceptLoopTask is not null)
        {
            try { await _acceptLoopTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _acceptLoopTask = null;
        _cts?.Dispose();
        _cts = null;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pipe = CreateServerPipe(_pipeName!);
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                _ = HandleConnectionAsync(pipe, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (IOException ex) when (IsPipeInstancesBusy(ex))
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"MCP accept loop error: {ex.Message}");
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        var sessionId = Interlocked.Increment(ref _nextSessionId);
        McpPipeSession? pipeSession = null;
        try
        {
            pipeSession = McpPipeSession.Start(pipe, mcpHandler, ct);
            _sessions[sessionId] = pipeSession;
            connectionTracker.SetMcpClientCount(_sessions.Count);
#if DEBUG
            logger.ZLogInformation($"MCP client connected. Active sessions: {_sessions.Count}");
#endif
            await pipeSession.Completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.ZLogWarning($"MCP session ended: {ex.Message}");
        }
        finally
        {
            if (pipeSession is not null)
            {
                _sessions.TryRemove(sessionId, out _);
                await pipeSession.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }

#if DEBUG
            logger.ZLogInformation($"MCP client disconnected. Active sessions: {_sessions.Count}");
#endif
            connectionTracker.SetMcpClientCount(_sessions.Count);
        }
    }

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        try
        {
            primitiveDispatcher.ClearCaches();
            toolsetContextManager.Clear();
            _ = BroadcastCatalogListChangedNotificationsAsync();
            logger.ZLogInformation(
                $"MCP host catalog reloaded ({catalogStore.ToolDescriptors.Count} tools, {catalogStore.ResourceDescriptors.Count} resources).");
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"Failed to reload MCP host catalog");
        }
    }

    private async Task BroadcastCatalogListChangedNotificationsAsync()
    {
        foreach (var session in _sessions.Values)
        {
            try
            {
                await session.SendNotificationAsync(NotificationMethods.ToolListChangedNotification).ConfigureAwait(false);
                await session.SendNotificationAsync(NotificationMethods.ResourceListChangedNotification).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"Failed to notify MCP client of catalog change: {ex.Message}");
            }
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
        catalogStore.CatalogChanged -= OnCatalogChanged;
        _cts?.Cancel();
        foreach (var session in _sessions.Values)
            session.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _sessions.Clear();
        _cts?.Dispose();
    }
}
