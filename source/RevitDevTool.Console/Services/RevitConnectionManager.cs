using System.Diagnostics;
using H.Formatters;
using H.Pipes;
using H.Pipes.Args;
using MessagePack;
using RevitDevTool.Bridge;
using RevitDevTool.Bridge.Abstractions;
using RevitDevTool.Bridge.IPC;
using RevitDevTool.Console.Services.Hosting;

namespace RevitDevTool.Console.Services;

/// <summary>
/// Manages pipe connections to multiple host application instances.
/// Application-agnostic — uses <see cref="IHostDiscovery"/> and <see cref="IHostInstance"/>.
/// Uses MessagePack for IPC serialization.
/// </summary>
public sealed class RevitConnectionManager : IAsyncDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan JobExecutionTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ShutdownAckTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(15);

    private readonly IHostDiscovery _discovery;
    private readonly Dictionary<string, PipeClient<PipeMessage>> _connections = new();
    private readonly Dictionary<string, TaskCompletionSource<PipeMessage>> _pendingRequests = new();
    private readonly Lock _lock = new();

    public event Action<IHostInstance, PipeProgress>? OnProgress;
    public event Action<IHostInstance?, PipeLogEntry>? OnHostLog;

    public RevitConnectionManager(IHostDiscovery discovery)
    {
        _discovery = discovery;
    }

    /// <summary>
    /// Discover all live pipes and connect to each one.
    /// </summary>
    public async Task DiscoverAndConnectAsync(CancellationToken ct = default)
    {
        foreach (var instance in _discovery.Discover())
        {
            await ConnectAsync(instance, ct).ConfigureAwait(false);
        }
    }

    public async Task ConnectAsync(IHostInstance instance, CancellationToken ct = default)
    {
        if (_connections.ContainsKey(instance.PipeName)) return;

        var client = new PipeClient<PipeMessage>(instance.PipeName,
            formatter: new MessagePackFormatter());

        client.MessageReceived += OnClientMessageReceived;
        client.ExceptionOccurred += (_, e) =>
            System.Console.Error.WriteLine($"[Pipe:{instance.PipeName}] Error: {e.Exception.Message}");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ConnectTimeout);

        try
        {
            await client.ConnectAsync(cts.Token).ConfigureAwait(false);
            _connections[instance.PipeName] = client;
            System.Console.WriteLine($"Connected to {instance.PipeName}");
        }
        catch (OperationCanceledException)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw new TimeoutException(
                $"Failed to connect to {instance.PipeName} within {ConnectTimeout.TotalSeconds}s");
        }
    }

    public async Task<JobResult> ExecuteJobAsync(
        IHostInstance instance, ResolvedJob job, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(instance.PipeName, out var client))
            throw new InvalidOperationException($"Not connected to {instance.PipeName}");

        var msg = new PipeMessage
        {
            Type = PipeMessageType.ExecuteJob,
            Payload = MessagePackSerializer.Serialize(job)
        };

        var tcs = new TaskCompletionSource<PipeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            _pendingRequests[msg.Id] = tcs;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(JobExecutionTimeout);
        using var crashWatcherCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);

        await using (cts.Token.Register(() => tcs.TrySetCanceled()))
        {
            try
            {
                System.Console.WriteLine($"Sending job to {instance.PipeName}...");
                await client.WriteAsync(msg, cts.Token).ConfigureAwait(false);
                System.Console.WriteLine("Waiting for response...");
                var crashWatcherTask = RevitCrashWatcher.MonitorAsync(
                    instance.ProcessId,
                    instance.HostVersion,
                    crashWatcherCts.Token);
                var completed = await Task.WhenAny(tcs.Task, crashWatcherTask).ConfigureAwait(false);

                if (completed == crashWatcherTask)
                {
                    lock (_lock) { _pendingRequests.Remove(msg.Id); }
                    await crashWatcherTask.ConfigureAwait(false);
                    return new JobResult
                    {
                        Success = false,
                        Error = $"Host process crash detected for Revit {instance.HostVersion} (PID {instance.ProcessId})."
                    };
                }

                await crashWatcherCts.CancelAsync().ConfigureAwait(false);
                await AwaitCrashWatcherStoppedAsync(crashWatcherTask).ConfigureAwait(false);
                var response = await tcs.Task.ConfigureAwait(false);
                System.Console.WriteLine($"Received response: {response.Type}");

                return response.Type switch
                {
                    PipeMessageType.JobCompleted => DeserializeResult(response.Payload, fallbackSuccess: true),
                    PipeMessageType.JobFailed => DeserializeResult(response.Payload, fallbackSuccess: false),
                    _ => new JobResult { Success = false, Error = $"Unexpected response: {response.Type}" }
                };
            }
            catch (OperationCanceledException)
            {
                lock (_lock) { _pendingRequests.Remove(msg.Id); }
                return new JobResult
                {
                    Success = false,
                    Error = $"Job execution timeout ({JobExecutionTimeout.TotalMinutes}min)"
                };
            }
        }
    }

    public List<IHostInstance> GetConnectedInstances(string version)
    {
        return _connections.Keys
            .Select(ParsePipeName)
            .Where(inst => inst != null && inst.HostVersion == version)
            .Cast<IHostInstance>()
            .ToList();
    }

    public List<IHostInstance> GetAllConnectedInstances()
    {
        return _connections.Keys
            .Select(ParsePipeName)
            .Where(inst => inst != null)
            .Cast<IHostInstance>()
            .ToList();
    }

    /// <summary>
    /// Send a graceful shutdown request and wait for acknowledgment.
    /// </summary>
    public async Task ShutdownAsync(IHostInstance instance, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(instance.PipeName, out var client))
            return;

        var msg = new PipeMessage { Type = PipeMessageType.Shutdown };
        var tcs = new TaskCompletionSource<PipeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            _pendingRequests[msg.Id] = tcs;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ShutdownAckTimeout);

        try
        {
            System.Console.WriteLine($"Sending shutdown to {instance.PipeName}...");
            await client.WriteAsync(msg, cts.Token).ConfigureAwait(false);

            await using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                var response = await tcs.Task.ConfigureAwait(false);
                if (response.Type == PipeMessageType.ShutdownAck)
                    System.Console.WriteLine(
                        $"{instance.AppId} {instance.HostVersion} (PID {instance.ProcessId}) shutdown acknowledged.");
            }
        }
        catch (OperationCanceledException)
        {
            lock (_lock) { _pendingRequests.Remove(msg.Id); }
            System.Console.WriteLine($"Shutdown timeout for {instance.PipeName}");
        }
        finally
        {
            _connections.Remove(instance.PipeName);
            await client.DisposeAsync().ConfigureAwait(false);
        }

        var exited = await WaitForProcessExitAsync(instance.ProcessId, ProcessExitTimeout, ct)
            .ConfigureAwait(false);
        if (!exited)
        {
            System.Console.WriteLine(
                $"[Shutdown] Process still alive after timeout (PID {instance.ProcessId}). " +
                "Potential in-host shutdown issue (graceful close not completed).");
            await TryCloseMainWindowFromOrchestratorAsync(instance.ProcessId, ct).ConfigureAwait(false);

            System.Console.WriteLine(RevitCrashWatcher.TryGetCrashSignal(instance.ProcessId, instance.HostVersion, out var crashReason) 
                ? $"[CrashRisk] {crashReason}" 
                : $"[CrashRisk] Revit {instance.HostVersion} PID {instance.ProcessId} remains alive after shutdown flow.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _connections.Values)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
        _connections.Clear();
    }

    // ── Private helpers ──────────────────────────────────────────────

    private void OnClientMessageReceived(object? sender, ConnectionMessageEventArgs<PipeMessage?> e)
    {
        var msg = e.Message;
        switch (msg)
        {
            case null:
                return;
            case { Type: PipeMessageType.LogChunk, Payload: not null }:
            {
                var log = MessagePackSerializer.Deserialize<PipeLogEntry>(msg.Payload);
                var prefix = string.IsNullOrWhiteSpace(log.Source) ? "[HostLog]" : $"[HostLog:{log.Source}]";
                System.Console.WriteLine($"{prefix} [{log.Level}] {log.Message}");
                if (!string.IsNullOrWhiteSpace(log.Exception))
                    System.Console.WriteLine(log.Exception);
                var instance = ParsePipeName(e.Connection.PipeName);
                OnHostLog?.Invoke(instance, log);
                return;
            }
            case { Type: PipeMessageType.Progress, Payload: not null }:
            {
                var progress = MessagePackSerializer.Deserialize<PipeProgress>(msg.Payload);
                var instance = ParsePipeName(e.Connection.PipeName);
                if (instance != null)
                    OnProgress?.Invoke(instance, progress);
                return;
            }
        }

        lock (_lock)
        {
            if (!_pendingRequests.Remove(msg.Id, out var tcs)) return;
            tcs.TrySetResult(msg);
        }
    }

    private static JobResult DeserializeResult(byte[]? payload, bool fallbackSuccess)
    {
        if (payload is { Length: > 0 })
            return MessagePackSerializer.Deserialize<JobResult>(payload);

        return fallbackSuccess
            ? new JobResult { Success = true }
            : new JobResult { Success = false, Error = "Unknown error" };
    }

    private static IHostInstance? ParsePipeName(string pipeName)
    {
        return !PipeNaming.TryParse(pipeName, out _, out var version, out var pid)
            ? null
            : new RevitHostInstance(version, pid, pipeName);
    }

    private static async Task<bool> WaitForProcessExitAsync(int pid, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            System.Console.WriteLine($"Process (PID {pid}) has exited.");
            return true;
        }
        catch (ArgumentException)
        {
            // Already gone
            return true;
        }
        catch (OperationCanceledException)
        {
            System.Console.WriteLine($"Process (PID {pid}) did not exit within {timeout.TotalSeconds}s.");
            return false;
        }
    }

    private static async Task TryCloseMainWindowFromOrchestratorAsync(int pid, CancellationToken ct)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            process.Refresh();
            if (process.HasExited)
                return;

            if (!process.CloseMainWindow())
            {
                System.Console.WriteLine($"[Shutdown] CloseMainWindow request was not accepted (PID {pid}).");
                return;
            }

            System.Console.WriteLine($"[Shutdown] CloseMainWindow requested by orchestrator (PID {pid}).");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            System.Console.WriteLine($"[Shutdown] Process exited after orchestrator close request (PID {pid}).");
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
        catch (OperationCanceledException)
        {
            System.Console.WriteLine(
                $"[Shutdown] Process still running after orchestrator close request (PID {pid}).");
        }
    }

    private static async Task AwaitCrashWatcherStoppedAsync(Task crashWatcherTask)
    {
        try
        {
            await crashWatcherTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when job finished before crash.
        }
    }
}
