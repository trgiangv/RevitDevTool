using System.Diagnostics;
using H.Formatters;
using H.Pipes;
using H.Pipes.Args;
using MessagePack;
using RevitDevTool.Bridge;
using RevitDevTool.Bridge.IPC;

namespace RevitDevTool.Engine;

/// <summary>
/// Named pipe server hosted inside Revit.
/// Receives <see cref="PipeMessage"/> from Console/Processor and dispatches
/// script execution to the addin's existing executors via a delegate.
/// Uses MessagePack for IPC serialization.
/// </summary>
public sealed class EngineHost : IDisposable
{
    private static readonly TimeSpan ShutdownResponseDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ShutdownRetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ShutdownFinalGrace = TimeSpan.FromSeconds(5);
    private const int MaxShutdownAttempts = 10;
    private const string ForceKillEnvName = "RDT_FORCE_KILL_ON_SHUTDOWN";

    private PipeServer<PipeMessage>? _server;
    private PipeConnection<PipeMessage?>? _lastConnection;
    private bool _disposed;

    public static EngineHost Instance { get; } = new();

    /// <summary>
    /// Delegate that opens the document, runs the script, and returns a <see cref="JobResult"/>.
    /// Set by the addin at startup. Runs on the Revit main thread via Revit.Async.
    /// </summary>
    public Func<ResolvedJob, Task<JobResult>>? ExecuteJobHandler { get; set; }

    private string PipeName { get; set; } = "";

    public async Task StartAsync(string appId, string version, int processId)
    {
        if (_server != null) return;

        PipeName = PipeNaming.Build(appId, version, processId);
        _server = new PipeServer<PipeMessage>(PipeName, formatter: new MessagePackFormatter());

        _server.MessageReceived += OnMessageReceived;
        _server.ExceptionOccurred += OnExceptionOccurred;
        _server.ClientConnected += (_, _) => Trace.TraceInformation($"[EngineHost] Client connected to {PipeName}");
        _server.ClientDisconnected += (_, _) => Trace.TraceInformation($"[EngineHost] Client disconnected from {PipeName}");

        await _server.StartAsync().ConfigureAwait(false);
        Trace.TraceInformation($"[EngineHost] Pipe server started: {PipeName}");
    }

    private async void OnMessageReceived(object? sender, ConnectionMessageEventArgs<PipeMessage?> e)
    {
        try
        {
            _lastConnection = e.Connection;
            var msg = e.Message;
            if (msg == null) return;

            switch (msg.Type)
            {
                case PipeMessageType.Ping:
                    await SendAsync(e.Connection, new PipeMessage
                    {
                        Id = msg.Id,
                        Type = PipeMessageType.Pong
                    }).ConfigureAwait(false);
                    break;

                case PipeMessageType.ExecuteJob:
                    await HandleExecuteJobAsync(msg, e.Connection).ConfigureAwait(false);
                    break;

                case PipeMessageType.Shutdown:
                    await HandleShutdownAsync(msg, e.Connection).ConfigureAwait(false);
                    break;

                case PipeMessageType.CancelJob:
                case PipeMessageType.Pong:
                case PipeMessageType.Progress:
                case PipeMessageType.LogChunk:
                case PipeMessageType.JobCompleted:
                case PipeMessageType.JobFailed:
                case PipeMessageType.ShutdownAck:
                    break;
                default:
                    throw new ArgumentOutOfRangeException("[EngineHost] Unknown message type: " + msg.Type);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[EngineHost] Error processing message: {ex.Message}");
        }
    }

    private async Task HandleExecuteJobAsync(PipeMessage msg, PipeConnection<PipeMessage?> connection)
    {
        Trace.TraceInformation($"[EngineHost] Received ExecuteJob: {msg.Id}");

        ResolvedJob? job = null;
        if (msg.Payload != null)
            job = MessagePackSerializer.Deserialize<ResolvedJob>(msg.Payload);

        if (job == null)
        {
            await SendJobFailedAsync(connection, msg.Id, "Failed to deserialize job payload", null, 0)
                .ConfigureAwait(false);
            return;
        }

        Trace.TraceInformation($"[EngineHost] Job: Script={job.Script}, File={job.FilePath}");

        var handler = ExecuteJobHandler;
        if (handler == null)
        {
            await SendJobFailedAsync(connection, msg.Id, "ExecuteJobHandler not configured", null, 0)
                .ConfigureAwait(false);
            return;
        }

        JobResult result;
        try
        {
            result = await handler(job).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[EngineHost] Unhandled error: {ex.Message}");
            result = JobResult.Fail(ex, 0);
        }

        Trace.TraceInformation($"[EngineHost] Result: Success={result.Success}, Duration={result.DurationMs}ms");

        var responseType = result.Success ? PipeMessageType.JobCompleted : PipeMessageType.JobFailed;
        await SendAsync(connection, new PipeMessage
        {
            Id = msg.Id,
            Type = responseType,
            Payload = MessagePackSerializer.Serialize(result)
        }).ConfigureAwait(false);
    }

    private static async Task HandleShutdownAsync(PipeMessage msg, PipeConnection<PipeMessage?> connection)
    {
        Trace.TraceInformation($"[EngineHost] Received Shutdown request: {msg.Id}");

        await SendAsync(connection, new PipeMessage
        {
            Id = msg.Id,
            Type = PipeMessageType.ShutdownAck
        }).ConfigureAwait(false);

        Trace.TraceInformation("[EngineHost] Shutdown acknowledged, closing Revit gracefully...");

        _ = Task.Run(async () =>
        {
            await TryShutdownProcessAsync().ConfigureAwait(false);
        });
    }

    private static async Task TryShutdownProcessAsync()
    {
        await Task.Delay(ShutdownResponseDelay).ConfigureAwait(false);
        var currentProcess = Process.GetCurrentProcess();

        for (var i = 0; i < MaxShutdownAttempts; i++)
        {
            currentProcess.Refresh();
            if (currentProcess.HasExited)
                return;

            if (currentProcess.CloseMainWindow())
                Trace.TraceInformation("[EngineHost] CloseMainWindow requested, waiting for exit...");

            await Task.Delay(ShutdownRetryDelay).ConfigureAwait(false);
        }

        await Task.Delay(ShutdownFinalGrace).ConfigureAwait(false);
        currentProcess.Refresh();
        if (currentProcess.HasExited)
            return;

        if (!IsForceKillEnabled())
        {
            Trace.TraceWarning(
                $"[EngineHost] Revit did not exit gracefully. Skip force-kill by default. Set {ForceKillEnvName}=1 to enable hard termination.");
            return;
        }

        Trace.TraceWarning("[EngineHost] Revit did not exit gracefully, forcing shutdown by env policy...");
        currentProcess.Kill();
    }

    private static bool IsForceKillEnabled()
    {
        var value = Environment.GetEnvironmentVariable(ForceKillEnvName);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static Task SendJobFailedAsync(
        PipeConnection<PipeMessage?> connection, string msgId, string error, string? stackTrace, long durationMs)
    {
        var result = new JobResult
        {
            Success = false,
            Error = error,
            StackTrace = stackTrace,
            DurationMs = durationMs
        };
        return SendAsync(connection, new PipeMessage
        {
            Id = msgId,
            Type = PipeMessageType.JobFailed,
            Payload = MessagePackSerializer.Serialize(result)
        });
    }

    private static async Task SendAsync(PipeConnection<PipeMessage?> connection, PipeMessage message)
    {
        try
        {
            await connection.WriteAsync(message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[EngineHost] Failed to send message: {ex.Message}");
        }
    }

    /// <summary>
    /// Publishes a log payload to the connected orchestrator client.
    /// This is best-effort and ignored when no client is connected yet.
    /// </summary>
    public async ValueTask PublishLogAsync(PipeLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry == null) return;
        var connection = _lastConnection;
        if (connection == null) return;

        try
        {
            var message = new PipeMessage
            {
                Type = PipeMessageType.LogChunk,
                Payload = MessagePackSerializer.Serialize(entry)
            };
            await connection.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Avoid Trace here to prevent recursive re-entry through PipeLogTraceListener
            // when the pipe is already shutting down or disconnected.
            Debug.WriteLine($"[EngineHost] Failed to publish log chunk: {ex.Message}");
        }
    }

    private static void OnExceptionOccurred(object? sender, ExceptionEventArgs e)
    {
        Trace.TraceError($"[EngineHost] Pipe error: {e.Exception.Message}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _server?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[EngineHost] Error during dispose: {ex.Message}");
        }
        _server = null;
    }
}
